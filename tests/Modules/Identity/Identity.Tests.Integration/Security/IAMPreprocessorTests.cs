using System.Net;
using System.Net.Http.Json;
using AlphaZero.Modules.Courses.Presentation.Courses.AddItem;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using Identity.Tests.Integration.Abstractions;
using ErrorOr;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Identity.Tests.Integration.Security;

public class SecurityApiFactory : ApiFactory
{
    public FakePolicyEvaluatorService Evaluator { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        
        // Force Production to use IAMPreprocessor instead of IAMDevPreprocessor
        builder.UseEnvironment("Production"); 
        
        builder.ConfigureTestServices(services =>
        {
            // Replace the real evaluator with our fake one to inspect contexts
            services.RemoveAll<IPolicyEvaluatorService>();
            services.AddSingleton<IPolicyEvaluatorService>(Evaluator);
        });
    }
}

public class FakePolicyEvaluatorService : IPolicyEvaluatorService
{
    public AuthorizationContext? LastContext { get; private set; }
    public ErrorOr<Success> ResultToReturn { get; set; } = Result.Success;

    public Task<ErrorOr<Success>> Authorize(AuthorizationContext context)
    {
        LastContext = context;
        return Task.FromResult(ResultToReturn);
    }

    public void Reset()
    {
        LastContext = null;
        ResultToReturn = Result.Success;
    }
}

public class IAMPreprocessorTests : IClassFixture<SecurityApiFactory>
{
    private readonly SecurityApiFactory _factory;
    private readonly HttpClient _client;

    public IAMPreprocessorTests(SecurityApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _factory.Evaluator.Reset();
    }

    [Fact]
    public async Task AddLesson_Should_ResolveCorrectTenant_From_Resource()
    {
        // 1. Arrange: Setup Course in Tenant A
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        
        Guid courseId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AlphaZero.Modules.Courses.Infrastructure.Persistance.AppDbContext>();
            
            var subject = AlphaZero.Modules.Courses.Domain.Aggregates.Subject.Subject.Create(tenantA, "Security Subject", "Desc").Value;
            db.Subjects.Add(subject);
            await db.SaveChangesAsync();
            
            var course = AlphaZero.Modules.Courses.Domain.Aggregates.Courses.Course.Create(tenantA, "Secure Course", "Desc", subject.Id).Value;
            db.Courses.Add(course);
            await db.SaveChangesAsync();
            courseId = course.Id;
        }

        // 2. Act: Attempt to add lesson to Tenant A's course while acting as User in Tenant B
        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantB.ToString());
        
        // We set evaluator to return Forbidden to simulate horizontal breakout prevention
        _factory.Evaluator.ResultToReturn = Error.Forbidden("Access.Denied", "Cannot access cross-tenant resource");

        var request = new AddLessonRequest
        {
            CourseId = courseId,
            SectionId = Guid.NewGuid(),
            Title = "Hacked Lesson",
            VideoId = Guid.NewGuid()
        };

        var response = await _client.PostAsJsonAsync($"/courses/{courseId}/sections/{request.SectionId}/lessons", request);

        // 3. Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        
        // Verify that the evaluator was called with Tenant A's ID!
        // This is the CRITICAL part of the test: even though the user is in Tenant B,
        // the IAMPreprocessor correctly resolved that the resource belongs to Tenant A.
        _factory.Evaluator.LastContext.Should().NotBeNull();
        _factory.Evaluator.LastContext!.TenantId.Should().Be(tenantA);
        _factory.Evaluator.LastContext.ResourceType.Should().Be(ResourceType.Courses);
    }
}

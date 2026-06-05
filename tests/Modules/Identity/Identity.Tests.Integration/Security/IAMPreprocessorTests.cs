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
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using Microsoft.EntityFrameworkCore;
using AlphaZero.Modules.Identity.Domain.Models;
using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Modules.Identity.Infrastructure.Models;
using AlphaZero.Modules.Identity.Domain.Repositories;

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
    public async Task AddLesson_Should_EvaluateContext_With_SessionTenant()
    {
        // 1. Arrange: Setup Course in Tenant A
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var userId = Guid.NewGuid();
        
        Guid courseId;
        Guid tenantUserId;
        using (var scope = _factory.Services.CreateScope())
        {
            var coursesDb = scope.ServiceProvider.GetRequiredService<AlphaZero.Modules.Courses.Infrastructure.Persistance.AppDbContext>();
            var identityDb = scope.ServiceProvider.GetRequiredService<AlphaZero.Modules.Identity.Infrastructure.Persistance.AppDbContext>();
            
            await coursesDb.Database.MigrateAsync();
            await identityDb.Database.MigrateAsync();

            // Seed Course in Tenant A
            var subject = AlphaZero.Modules.Courses.Domain.Aggregates.Subject.Subject.Create(Guid.NewGuid(), tenantA, "Security Subject", "Desc").Value;
            coursesDb.Subjects.Add(subject);
            await coursesDb.SaveChangesAsync();
            
            var course = AlphaZero.Modules.Courses.Domain.Aggregates.Courses.Course.Create(Guid.NewGuid(), tenantA, "Secure Course", "Desc", subject.Id).Value;
            coursesDb.Courses.Add(course);
            await coursesDb.SaveChangesAsync();
            courseId = course.Id;

            // Seed User and Assignment in Tenant B (the acting tenant)
            var user = TenantUser.Create(tenantB, userId.ToString(), "Test User", TenantUserDeviceInfo.Empty).Value;
            tenantUserId = user.Id;
            identityDb.TenantUsers.Add(user);

            var principal = Principal.Create(Guid.NewGuid(), "test-role", "hash", "Custom", PrincipalType.User, null, tenantB).Value;
            var principalRepo = scope.ServiceProvider.GetRequiredService<IPrincipalRepository>();
            principalRepo.Add(principal);

            var assignment = TenantUserPrinciaplAssignment.Create(tenantB, user, principal, $"az:courses:{tenantB}:course/{course.Id}").Value;
            identityDb.TenantPrinciaplAssignments.Add(assignment);

            await identityDb.SaveChangesAsync();
        }

        // 2. Act: Attempt to add lesson to Tenant A's course while acting as User in Tenant B
        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantB.ToString());
        
        // We set the auth claim headers that TestAuthHandler uses.
        // We use user.Id because CurrentTenantUserRepository searches by primary key.
        _client.DefaultRequestHeaders.Remove("X-Test-User-Id");
        _client.DefaultRequestHeaders.Add("X-Test-User-Id", tenantUserId.ToString());

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
        
        // Verify that the evaluator was called with Tenant B's ID (the session tenant)
        // because we no longer resolve the resource tenant from the database in the preprocessor.
        _factory.Evaluator.LastContext.Should().NotBeNull();
        _factory.Evaluator.LastContext!.TenantId.Should().Be(tenantB);
        _factory.Evaluator.LastContext.ResourceType.Should().Be(ResourceType.Courses);
    }
}

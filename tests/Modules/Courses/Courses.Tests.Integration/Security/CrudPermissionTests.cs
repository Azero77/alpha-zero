using System.Net;
using System.Net.Http.Json;
using AlphaZero.Modules.Courses.Presentation.Courses.Create;
using AlphaZero.Modules.Courses.Presentation.Subjects.Create;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using Courses.Tests.Integration.Abstractions;
using ErrorOr;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

// The class fixture below runs a second WebApplicationFactory (IAM-enforcing host). Both fixtures
// share process-global connection-string environment variables at startup, so this assembly must
// not run test collections in parallel — otherwise the two hosts can migrate the same database
// twice (relation-already-exists races observed under parallelization).
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace Courses.Tests.Integration.Security;

/// <summary>
/// Copy of the Identity module's SecurityApiFactory prior art: keeps the Development host but turns
/// the IAM dev bypass off so requests flow through IAMPreprocessor, and swaps the real policy
/// evaluator for a controllable fake. Used to prove the module's AccessControl requirement wiring
/// (permission action + resource ARN) on the new CRUD endpoints' permission-denial path.
/// </summary>
public class CrudSecurityApiFactory : ApiFactory
{
    public FakePolicyEvaluatorService Evaluator { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Testing:BypassIAM", "false");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPolicyEvaluatorService>();
            services.AddSingleton<IPolicyEvaluatorService>(Evaluator);
        });
    }
}

public class FakePolicyEvaluatorService : IPolicyEvaluatorService
{
    public ErrorOr<Success> ResultToReturn { get; set; } = ErrorOr.Result.Success;

    public Task<ErrorOr<Success>> Authorize(AuthorizationContext context) =>
        Task.FromResult(ResultToReturn);
}

public class CrudPermissionTests : IClassFixture<CrudSecurityApiFactory>, IDisposable
{
    private readonly CrudSecurityApiFactory _factory;
    private readonly HttpClient _client;

    public CrudPermissionTests(CrudSecurityApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public void Dispose()
    {
        _factory.Evaluator.ResultToReturn = ErrorOr.Result.Success;
    }

    private void SetTenant(Guid tenantId)
    {
        _client.DefaultRequestHeaders.Remove("X-TenantId");
        _client.DefaultRequestHeaders.Add("X-TenantId", tenantId.ToString());
    }

    private async Task<(Guid CourseId, Guid SubjectId)> SeedCourse(Guid tenantId)
    {
        SetTenant(tenantId);
        var subResp = await _client.PostAsJsonAsync("/courses/subjects", new CreateSubjectRequest { Name = "CS" });
        var subjectId = (await subResp.Content.ReadFromJsonAsync<CreateSubjectResponse>())!.Id;
        var courseResp = await _client.PostAsJsonAsync("/courses", new CreateCourseRequest { Title = "Sec Course", SubjectId = subjectId });
        var courseId = (await courseResp.Content.ReadFromJsonAsync<CreateCourseResponse>())!.Id;
        return (courseId, subjectId);
    }

    [Fact]
    public async Task NewEndpoints_Should_Return403_WhenPolicyDenies()
    {
        // Arrange: policy denied across the board
        _factory.Evaluator.ResultToReturn = Error.Forbidden("Access.Denied", "Test denial");
        var courseId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var enrollmentId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();

        // Act: every new write/list route hits its AccessControl requirement
        var update = await _client.PutAsJsonAsync($"/courses/{courseId}", new { CourseId = courseId, Title = "T", SubjectId = subjectId });
        var rename = await _client.PutAsJsonAsync($"/courses/{courseId}/sections/{sectionId}", new { CourseId = courseId, SectionId = sectionId, Title = "T" });
        var deleteSection = await _client.DeleteAsync($"/courses/{courseId}/sections/{sectionId}");
        var archive = await _client.PatchAsJsonAsync($"/courses/{courseId}/archive", new { });
        var deleteCourse = await _client.DeleteAsync($"/courses/{courseId}");
        var updateSubject = await _client.PutAsJsonAsync($"/courses/subjects/{subjectId}", new { Id = subjectId, Name = "N" });
        var deleteSubject = await _client.DeleteAsync($"/courses/subjects/{subjectId}");
        var deactivate = await _client.PostAsJsonAsync($"/courses/enrollments/{enrollmentId}/deactivate", new { });
        var list = await _client.GetAsync($"/courses/enrollments?StudentId={Guid.NewGuid()}");

        // Assert
        update.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        rename.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        deleteSection.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        archive.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        deleteCourse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        updateSubject.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        deleteSubject.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        deactivate.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        list.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Endpoints_Should_Pass_WhenPolicyAllows()
    {
        // Arrange: policy granted — proves the denials above come from the evaluator wiring, not a host misconfiguration
        var tenantId = Guid.NewGuid();
        var (courseId, subjectId) = await SeedCourse(tenantId);

        // Act
        var update = await _client.PutAsJsonAsync($"/courses/{courseId}", new { CourseId = courseId, Title = "Updated", SubjectId = subjectId });

        // Assert
        update.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}

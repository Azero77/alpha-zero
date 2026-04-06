using System.Net;
using System.Net.Http.Json;
using AlphaZero.Modules.Courses.Application.Subjects.Queries.GetSubject;
using AlphaZero.Modules.Courses.Presentation.Subjects.Create;
using AlphaZero.Shared.Queries;
using Courses.Tests.Integration.Abstractions;
using FluentAssertions;

namespace Courses.Tests.Integration;

public class SubjectTests : BaseIntegrationTest
{
    public SubjectTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task CreateSubject_Should_ReturnCreated_WhenRequestIsValid()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var request = new CreateSubjectRequest { Name = "Math", Description = "Mathematics" };

        // Act
        var response = await Client.PostAsJsonAsync("/courses/subjects", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<CreateSubjectResponse>();
        result.Should().NotBeNull();
        result!.Id.Should().NotBeEmpty();

        // Verify in DB
        var subject = await DbContext.Subjects.FindAsync(result.Id);
        subject.Should().NotBeNull();
        subject!.Name.Should().Be("Math");
        subject.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task GetSubject_Should_ReturnSubject_WhenExists()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        var createRequest = new CreateSubjectRequest { Name = "Science", Description = "Science Subject" };
        var createResponse = await Client.PostAsJsonAsync("/courses/subjects", createRequest);
        var subjectId = (await createResponse.Content.ReadFromJsonAsync<CreateSubjectResponse>())!.Id;

        // Act
        var response = await Client.GetAsync($"/courses/subjects/{subjectId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var subject = await response.Content.ReadFromJsonAsync<SubjectDto>();
        subject.Should().NotBeNull();
        subject!.Id.Should().Be(subjectId);
        subject.Name.Should().Be("Science");
    }

    [Fact]
    public async Task ListSubjects_Should_ReturnPaginatedSubjects()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        SetTenant(tenantId);
        for (int i = 1; i <= 5; i++)
        {
            await Client.PostAsJsonAsync("/courses/subjects", new CreateSubjectRequest { Name = $"Subject {i}" });
        }

        // Act
        var response = await Client.GetAsync("/courses/subjects?Page=1&PerPage=3");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<SubjectDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(3);
        result.TotalCount.Should().Be(5);
    }

    [Fact]
    public async Task Subject_Isolation_Should_EnsureTenantSeparation()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        SetTenant(tenantA);
        await Client.PostAsJsonAsync("/courses/subjects", new CreateSubjectRequest { Name = "Tenant A Subject" });

        SetTenant(tenantB);
        await Client.PostAsJsonAsync("/courses/subjects", new CreateSubjectRequest { Name = "Tenant B Subject" });

        // Act
        SetTenant(tenantA);
        var responseA = await Client.GetAsync("/courses/subjects");
        var resultA = await responseA.Content.ReadFromJsonAsync<PagedResult<SubjectDto>>();

        SetTenant(tenantB);
        var responseB = await Client.GetAsync("/courses/subjects");
        var resultB = await responseB.Content.ReadFromJsonAsync<PagedResult<SubjectDto>>();

        // Assert
        resultA!.Items.Should().ContainSingle(s => s.Name == "Tenant A Subject");
        resultA.Items.Should().NotContain(s => s.Name == "Tenant B Subject");

        resultB!.Items.Should().ContainSingle(s => s.Name == "Tenant B Subject");
        resultB.Items.Should().NotContain(s => s.Name == "Tenant A Subject");
    }
}

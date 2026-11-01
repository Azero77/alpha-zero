using AlphaZero.Modules.Courses.Infrastructure.RequestResponseMessaging;
using AlphaZero.Modules.Identity.IntegrationEvents;
using FluentAssertions;
using MassTransit;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Infrastructure;

public class CoursePlanPrincipalServiceTests
{
    private readonly IRequestClient<EnsureCoursePlanPrincipalRequest> _requestClient;
    private readonly CoursePlanPrincipalService _service;

    private static readonly Guid CourseId = Guid.NewGuid();
    private static readonly Guid PlanId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid ExpectedPrincipalId = Guid.NewGuid();

    public CoursePlanPrincipalServiceTests()
    {
        _requestClient = Substitute.For<IRequestClient<EnsureCoursePlanPrincipalRequest>>();
        _service = new CoursePlanPrincipalService(_requestClient);
    }

    [Fact]
    public async Task EnsurePlanPrincipalAsync_ShouldReturnPrincipalId_WhenSuccessResponseReceived()
    {
        // Arrange
        var mockSuccess = Substitute.For<Response<CoursePlanPrincipalEnsuredResponse>>();
        mockSuccess.Message.Returns(new CoursePlanPrincipalEnsuredResponse(ExpectedPrincipalId));

        var response = new Response<CoursePlanPrincipalEnsuredResponse, CoursePlanPrincipalEnsureFailedResponse>(
            Task.FromResult(mockSuccess),
            new TaskCompletionSource<Response<CoursePlanPrincipalEnsureFailedResponse>>().Task);

        _requestClient.GetResponse<CoursePlanPrincipalEnsuredResponse, CoursePlanPrincipalEnsureFailedResponse>(
            Arg.Is<EnsureCoursePlanPrincipalRequest>(r => r.CourseId == CourseId && r.PlanId == PlanId && r.TenantId == TenantId),
            Arg.Any<CancellationToken>())
            .Returns(response);

        // Act
        var result = await _service.EnsurePlanPrincipalAsync(CourseId, PlanId, TenantId);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().Be(ExpectedPrincipalId);
    }

    [Fact]
    public async Task EnsurePlanPrincipalAsync_ShouldReturnError_WhenFailureResponseReceived()
    {
        // Arrange
        var mockFailure = Substitute.For<Response<CoursePlanPrincipalEnsureFailedResponse>>();
        mockFailure.Message.Returns(new CoursePlanPrincipalEnsureFailedResponse("Policy creation failed", "Principal.PolicyFailed"));

        var response = new Response<CoursePlanPrincipalEnsuredResponse, CoursePlanPrincipalEnsureFailedResponse>(
            new TaskCompletionSource<Response<CoursePlanPrincipalEnsuredResponse>>().Task,
            Task.FromResult(mockFailure));

        _requestClient.GetResponse<CoursePlanPrincipalEnsuredResponse, CoursePlanPrincipalEnsureFailedResponse>(
            Arg.Any<EnsureCoursePlanPrincipalRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(response);

        // Act
        var result = await _service.EnsurePlanPrincipalAsync(CourseId, PlanId, TenantId);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Principal.PolicyFailed");
        result.FirstError.Description.Should().Be("Policy creation failed");
    }
}

using AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourseView;
using AlphaZero.Modules.Courses.Application.Queries;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Courses;

// #32: mirrors GetCourseOverviewPreRequestHandlerTests — the view pre-request handler
// extends the module's existing visibility precedent to the whole course detail.
public class GetCourseViewPreRequestHandlerTests
{
    private readonly ICourseQueryService _courseQueryService;
    private readonly IPolicyEvaluatorService _policyEvaluator;
    private readonly GetCourseViewPreRequestHandler _handler;

    private readonly Guid _courseId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    public GetCourseViewPreRequestHandlerTests()
    {
        _courseQueryService = Substitute.For<ICourseQueryService>();
        _policyEvaluator = Substitute.For<IPolicyEvaluatorService>();

        _handler = new GetCourseViewPreRequestHandler(_courseQueryService, _policyEvaluator);
    }

    [Fact]
    public async Task PreHandle_WhenCourseNotFound_ReturnsNotFound()
    {
        _courseQueryService.GetCourseStatusAsync(_courseId, Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var result = await _handler.PreHandle(new GetCourseViewQuery(_courseId, Guid.Empty), null, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task PreHandle_WhenCourseIsPublished_ReturnsSuccess_EvenWhenAnonymous()
    {
        _courseQueryService.GetCourseStatusAsync(_courseId, Arg.Any<CancellationToken>())
            .Returns("Published");

        var result = await _handler.PreHandle(new GetCourseViewQuery(_courseId, Guid.Empty), null, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().Be(Result.Success);
    }

    [Fact]
    public async Task PreHandle_WhenCourseIsDraft_AndContextIsNull_ReturnsNotFoundToMaskExistence()
    {
        _courseQueryService.GetCourseStatusAsync(_courseId, Arg.Any<CancellationToken>())
            .Returns("Draft");

        var result = await _handler.PreHandle(new GetCourseViewQuery(_courseId, Guid.Empty), null, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task PreHandle_WhenCourseIsDraft_AndCallerUnauthorized_ReturnsNotFoundToMaskExistence()
    {
        _courseQueryService.GetCourseStatusAsync(_courseId, Arg.Any<CancellationToken>())
            .Returns("Draft");

        var context = new AuthorizationContext
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            AuthenticationMethod = "TenantUser"
        };

        _policyEvaluator.Authorize(Arg.Any<AuthorizationContext>())
            .Returns(Error.Forbidden("Access.Denied", "Forbidden"));

        var result = await _handler.PreHandle(new GetCourseViewQuery(_courseId, Guid.Empty), context, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task PreHandle_WhenCourseIsDraft_AndCallerAuthorized_ReturnsSuccess()
    {
        _courseQueryService.GetCourseStatusAsync(_courseId, Arg.Any<CancellationToken>())
            .Returns("Draft");

        var context = new AuthorizationContext
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            AuthenticationMethod = "TenantUser"
        };

        _policyEvaluator
            .When(r => r.Authorize(Arg.Any<AuthorizationContext>()))
            .Do(r => r.Arg<AuthorizationContext>().RequiredPermission.Should().Be("courses:View"));
        _policyEvaluator.Authorize(Arg.Any<AuthorizationContext>())
            .Returns(Result.Success);

        var result = await _handler.PreHandle(new GetCourseViewQuery(_courseId, Guid.Empty), context, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().Be(Result.Success);
    }
}

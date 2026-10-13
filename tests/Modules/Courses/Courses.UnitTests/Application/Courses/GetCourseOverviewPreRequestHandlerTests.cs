using AlphaZero.Modules.Courses.Application.Courses.Queries.GetCourseOverview;
using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Courses;

public class GetCourseOverviewPreRequestHandlerTests
{
    private readonly ICourseOverviewRepository _overviewRepository;
    private readonly IPolicyEvaluatorService _policyEvaluator;
    private readonly IAuthorizationContextFactory _authFactory;
    private readonly ITenantProvider _tenantProvider;
    private readonly GetCourseOverviewPreRequestHandler _handler;

    private readonly Guid _courseId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    public GetCourseOverviewPreRequestHandlerTests()
    {
        _overviewRepository = Substitute.For<ICourseOverviewRepository>();
        _policyEvaluator = Substitute.For<IPolicyEvaluatorService>();
        _authFactory = Substitute.For<IAuthorizationContextFactory>();
        _tenantProvider = Substitute.For<ITenantProvider>();
        _tenantProvider.GetTenant().Returns(_tenantId);

        _handler = new GetCourseOverviewPreRequestHandler(
            _overviewRepository,
            _policyEvaluator,
            _authFactory,
            _tenantProvider);
    }

    private CourseOverviewDto CreateDto(string status) =>
        new(Guid.NewGuid(), _courseId, status, "Desc", null, null, null, "\"etag123\"");

    [Fact]
    public async Task PreHandle_WhenOverviewNotFound_ReturnsNotFound()
    {
        // Arrange
        _overviewRepository.GetByCourseIdAsync(_courseId, Arg.Any<CancellationToken>())
            .Returns((CourseOverviewDto?)null);

        // Act
        var result = await _handler.PreHandle(new GetCourseOverviewQuery(_courseId), null, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task PreHandle_WhenCourseIsPublished_ReturnsSuccess_EvenWhenAnonymous()
    {
        // Arrange
        var dto = CreateDto("Published");
        _overviewRepository.GetByCourseIdAsync(_courseId, Arg.Any<CancellationToken>())
            .Returns(dto);

        // Act
        var result = await _handler.PreHandle(new GetCourseOverviewQuery(_courseId), null, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().Be(Result.Success);
    }

    [Fact]
    public async Task PreHandle_WhenCourseIsDraft_AndContextIsNull_ReturnsNotFoundToMaskExistence()
    {
        // Arrange
        var dto = CreateDto("Draft");
        _overviewRepository.GetByCourseIdAsync(_courseId, Arg.Any<CancellationToken>())
            .Returns(dto);

        // Act
        var result = await _handler.PreHandle(new GetCourseOverviewQuery(_courseId), null, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task PreHandle_WhenCourseIsDraft_AndUserForbidden_ReturnsNotFoundToMaskExistence()
    {
        // Arrange
        var dto = CreateDto("Draft");
        _overviewRepository.GetByCourseIdAsync(_courseId, Arg.Any<CancellationToken>())
            .Returns(dto);

        var context = new AuthorizationContext
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            AuthenticationMethod = "TenantUser"
        };

        var targetContext = context with { RequiredPermission = "courses.overview:Manage" };
        _authFactory.GetOrCreateCurrentContextAsync("courses.overview:Manage", Arg.Any<ResourceArn>(), Arg.Any<CancellationToken>())
            .Returns(targetContext);

        _policyEvaluator.Authorize(targetContext)
            .Returns(Error.Forbidden("Access.Denied", "Forbidden"));

        // Act
        var result = await _handler.PreHandle(new GetCourseOverviewQuery(_courseId), context, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task PreHandle_WhenCourseIsDraft_AndUserAuthorized_ReturnsSuccess()
    {
        // Arrange
        var dto = CreateDto("Draft");
        _overviewRepository.GetByCourseIdAsync(_courseId, Arg.Any<CancellationToken>())
            .Returns(dto);

        var context = new AuthorizationContext
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            AuthenticationMethod = "Principal"
        };

        var targetContext = context with { RequiredPermission = "courses.overview:Manage" };
        _authFactory.GetOrCreateCurrentContextAsync("courses.overview:Manage", Arg.Any<ResourceArn>(), Arg.Any<CancellationToken>())
            .Returns(targetContext);

        _policyEvaluator.Authorize(targetContext)
            .Returns(Result.Success);

        // Act
        var result = await _handler.PreHandle(new GetCourseOverviewQuery(_courseId), context, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().Be(Result.Success);
    }
}

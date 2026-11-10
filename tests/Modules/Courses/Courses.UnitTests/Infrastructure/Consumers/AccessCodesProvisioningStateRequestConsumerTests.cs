using AlphaZero.Modules.Courses.Application.Enrollments.Queries.GetAccessCodesProvisioningState;
using AlphaZero.Modules.Courses.IntegrationEvents;
using AlphaZero.Modules.Courses.Infrastructure.Consumers;
using ErrorOr;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Infrastructure.Consumers;

/// <summary>
/// The consumer is a thin adapter: it guards the contract invariant (non-empty
/// AccessCodeIds) and delegates the record lookup to
/// <see cref="GetAccessCodesProvisioningStateQuery"/>. Business-logic coverage lives
/// in GetAccessCodesProvisioningStateQueryHandlerTests / the repository tests.
/// </summary>
public class AccessCodesProvisioningStateRequestConsumerTests
{
    private readonly ICoursesModule _coursesModule = Substitute.For<ICoursesModule>();
    private readonly ILogger<AccessCodesProvisioningStateRequestConsumer> _logger = Substitute.For<ILogger<AccessCodesProvisioningStateRequestConsumer>>();

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid AccessCodeId1 = Guid.NewGuid();
    private static readonly Guid AccessCodeId2 = Guid.NewGuid();

    private AccessCodesProvisioningStateRequestConsumer CreateConsumer() =>
        new(_coursesModule, _logger);

    private static ConsumeContext<GetAccessCodesProvisioningStateRequest> CreateContext(
        GetAccessCodesProvisioningStateRequest message)
    {
        var context = Substitute.For<ConsumeContext<GetAccessCodesProvisioningStateRequest>>();
        context.Message.Returns(message);
        context.CancellationToken.Returns(CancellationToken.None);
        return context;
    }

    [Fact]
    public async Task Consume_WithEmptyAccessCodeIds_ShouldRespondEmpty_WithoutDispatchingQuery()
    {
        var context = CreateContext(new GetAccessCodesProvisioningStateRequest(TenantId, []));

        await CreateConsumer().Consume(context);

        await context.Received().RespondAsync(
            Arg.Is<AccessCodesProvisioningStateResponse>(r => r.Items.Count == 0));
        await _coursesModule.DidNotReceive().Send(
            Arg.Any<GetAccessCodesProvisioningStateQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_ShouldDelegateToQuery_AndRespondWithItems()
    {
        var codeIds = new[] { AccessCodeId1, AccessCodeId2 };
        var items = new List<AccessCodeProvisioningStateItem>
        {
            new(AccessCodeId1,
                Guid.NewGuid(),
                "arn:course:" + AccessCodeId1,
                "Pending",
                null,
                1,
                DateTime.UtcNow,
                DateTime.UtcNow),
        };

        _coursesModule.Send(Arg.Any<GetAccessCodesProvisioningStateQuery>(), Arg.Any<CancellationToken>())
            .Returns(items);

        var context = CreateContext(new GetAccessCodesProvisioningStateRequest(TenantId, codeIds));

        await CreateConsumer().Consume(context);

        await _coursesModule.Received().Send(
            Arg.Is<GetAccessCodesProvisioningStateQuery>(q =>
                q.TenantId == TenantId &&
                q.AccessCodeIds.SequenceEqual(codeIds)),
            Arg.Any<CancellationToken>());

        await context.Received().RespondAsync(
            Arg.Is<AccessCodesProvisioningStateResponse>(r =>
                r.Items.Count == 1 && r.Items[0].AccessCodeId == AccessCodeId1));
    }

    [Fact]
    public async Task Consume_WhenQueryFails_ShouldRespondEmpty()
    {
        _coursesModule.Send(Arg.Any<GetAccessCodesProvisioningStateQuery>(), Arg.Any<CancellationToken>())
            .Returns(Error.Validation("TenantId", "TenantId must not be empty."));

        var context = CreateContext(new GetAccessCodesProvisioningStateRequest(Guid.Empty, [AccessCodeId1]));

        await CreateConsumer().Consume(context);

        await context.Received().RespondAsync(
            Arg.Is<AccessCodesProvisioningStateResponse>(r => r.Items.Count == 0));
    }
}

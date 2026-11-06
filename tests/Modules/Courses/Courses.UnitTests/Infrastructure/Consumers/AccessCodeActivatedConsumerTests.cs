using AlphaZero.Modules.Courses.Application.Enrollments.Commands.ProvisionAccessCode;
using AlphaZero.Modules.Courses.Infrastructure.Consumers;
using AlphaZero.Modules.Library.IntegrationEvents;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Infrastructure.Consumers;

/// <summary>
/// The consumer is a thin adapter: it resolves tenant scope and delegates all
/// provisioning to ProvisionAccessCodeCommand. Business-logic coverage lives in
/// ProvisionAccessCodeCommandHandlerTests.
/// </summary>
public class AccessCodeActivatedConsumerTests
{
    private readonly ICoursesModule _coursesModule = Substitute.For<ICoursesModule>();
    private readonly ITenantProvider _tenantProvider = Substitute.For<ITenantProvider>();
    private readonly IAmbientTenantContext _ambientTenant = new AmbientTenantContext();
    private readonly ILogger<AccessCodeActivatedConsumer> _logger = Substitute.For<ILogger<AccessCodeActivatedConsumer>>();

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CourseId = Guid.NewGuid();
    private static readonly Guid AccessCodeId = Guid.NewGuid();

    private AccessCodeActivatedConsumer CreateConsumer() =>
        new(_coursesModule, _tenantProvider, _ambientTenant, _logger);

    private static ConsumeContext<AccessCodeActivatedIntegrationEvent> CreateContext(AccessCodeActivatedIntegrationEvent message)
    {
        var context = Substitute.For<ConsumeContext<AccessCodeActivatedIntegrationEvent>>();
        context.Message.Returns(message);
        context.CancellationToken.Returns(CancellationToken.None);
        return context;
    }

    [Fact]
    public async Task Consume_Should_DelegateToProvisionAccessCodeCommand()
    {
        var targetArn = ResourceArn.ForCourse(TenantId, CourseId);
        var activatedEvent = new AccessCodeActivatedIntegrationEvent(
            Guid.NewGuid(),
            TenantId,
            AccessCodeId,
            UserId,
            targetArn,
            DateTime.UtcNow,
            Guid.NewGuid());

        _tenantProvider.GetTenant().Returns(TenantId);
        _coursesModule.Send(Arg.Any<ProvisionAccessCodeCommand>(), Arg.Any<CancellationToken>())
            .Returns(new Success());

        await CreateConsumer().Consume(CreateContext(activatedEvent));

        await _coursesModule.Received().Send(
            Arg.Is<ProvisionAccessCodeCommand>(c =>
                c.AccessCodeId == AccessCodeId &&
                c.RedeemingUserId == UserId &&
                c.TenantId == TenantId &&
                c.TargetResourceArn == targetArn),
            Arg.Any<CancellationToken>());
    }
}

using AlphaZero.Modules.Courses.IntegrationEvents;
using AlphaZero.Modules.Identity.Application;
using AlphaZero.Modules.Identity.Application.Principals.Commands.AssignPrincipalToUser;
using AlphaZero.Modules.Identity.Infrastructure.Consumers;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using MassTransit;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AlphaZero.Modules.Identity.UnitTests.Infrastructure.Consumers;

public class EnrollementCreatedIntegrationEventConsumerTests
{
    private readonly IIdentityModule _identityModule;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<EnrollementCreatedIntegrationEventConsumer> _logger;
    private readonly EnrollementCreatedIntegrationEventConsumer _consumer;

    private static readonly Guid StudentPrincipalTemplateId = Guid.Parse("00000000-0000-0000-0000-100000000002");
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid StudentId = Guid.NewGuid();
    private static readonly Guid CourseId = Guid.NewGuid();
    private static readonly Guid EnrollmentId = Guid.NewGuid();

    public EnrollementCreatedIntegrationEventConsumerTests()
    {
        _identityModule = Substitute.For<IIdentityModule>();
        _tenantProvider = Substitute.For<ITenantProvider>();
        _tenantProvider.GetTenant().Returns(TenantId);
        _logger = Substitute.For<ILogger<EnrollementCreatedIntegrationEventConsumer>>();

        _consumer = new EnrollementCreatedIntegrationEventConsumer(
            _identityModule,
            _tenantProvider,
            _logger);
    }

    [Fact]
    public async Task Consume_ShouldSendAssignPrincipalCommand_WithStudentRoleAndCourseArn()
    {
        // Arrange
        var context = Substitute.For<ConsumeContext<EnrollementCreatedIntegrationEvent>>();
        context.Message.Returns(new EnrollementCreatedIntegrationEvent(EnrollmentId, StudentId, CourseId, TenantId));

        _identityModule.Send(Arg.Any<AssignPrincipalToUserCommand>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());

        // Act
        await _consumer.Consume(context);

        // Assert
        var expectedArn = $"az:course:{TenantId}:course/{CourseId}";
        await _identityModule.Received(1).Send(Arg.Is<AssignPrincipalToUserCommand>(c =>
            c.TenantUserId == StudentId &&
            c.PrincipalId == StudentPrincipalTemplateId &&
            c.ResourceArn == expectedArn), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_ShouldFallbackToTenantProvider_WhenEventTenantIdIsEmpty()
    {
        // Arrange
        var context = Substitute.For<ConsumeContext<EnrollementCreatedIntegrationEvent>>();
        context.Message.Returns(new EnrollementCreatedIntegrationEvent(EnrollmentId, StudentId, CourseId, Guid.Empty));

        _identityModule.Send(Arg.Any<AssignPrincipalToUserCommand>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());

        // Act
        await _consumer.Consume(context);

        // Assert
        var expectedArn = $"az:course:{TenantId}:course/{CourseId}";
        await _identityModule.Received(1).Send(Arg.Is<AssignPrincipalToUserCommand>(c =>
            c.TenantUserId == StudentId &&
            c.PrincipalId == StudentPrincipalTemplateId &&
            c.ResourceArn == expectedArn), Arg.Any<CancellationToken>());
    }
}

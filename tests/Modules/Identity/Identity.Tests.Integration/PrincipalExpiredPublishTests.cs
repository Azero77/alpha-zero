using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Modules.Identity.Domain.Services;
using AlphaZero.Modules.Identity.IntegrationEvents;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Infrastructure.Security;
using AlphaZero.Shared.Security;
using FluentAssertions;
using Identity.Tests.Integration.Abstractions;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Xunit;

namespace Identity.Tests.Integration;

/// <summary>
/// Step 7 acceptance (issue #34 §4): an expired-window ticket evaluated by the real DI-wired
/// AccessTicketValid operator results in a PrincipalExpired publish, recorded by the test
/// publish observer (RecordingPublishObserver harness). Requires Docker (runs in CI).
/// </summary>
public class PrincipalExpiredPublishTests(ApiFactory factory) : BaseIntegrationTest(factory)
{
    private const string TestSecret = "integration-access-ticket-secret-min-32-chars!!!";

    [Fact]
    public async Task ExpiredWindowEvaluation_ShouldRaisePrincipalExpired_Publish()
    {
        // Arrange: a ticket whose ACCESS WINDOW has elapsed (ticket-exp itself still fresh),
        // minted under the host's configured AccessTicket:Secret (dev default in tests). The
        // ticket MUST be bound to the same actor/tenant as the AuthorizationContext — §3.3
        // evaluates actor binding BEFORE the window check, so a foreign ticket denies with
        // AccessTicketForeign and never reaches the lazy-expiry signal under test.
        var signer = new AccessTicketSigner(
            Options.Create(new AccessTicketSettings { Secret = "dev-access-ticket-secret-alphazero-default-change-in-prod-min32!" }));

        var context = new PrincipalUserAuthorizationContext
        {
            Id = Guid.NewGuid(),
            PrincipalId = Guid.NewGuid(),
            Username = "student",
            AuthenticationMethod = "Principal",
            TenantId = Guid.NewGuid(),
            RequiredPermission = "video:Stream",
            ResourcePath = "courses/course-1",
            ResourceType = "Courses"
        };

        var ticket = signer.GenerateTicket(
            tenantId: context.TenantId!.Value,
            userId: context.Id,
            enrollmentId: Guid.NewGuid(),
            windowStart: DateTimeOffset.UtcNow.AddDays(-90),
            windowEnd: DateTimeOffset.UtcNow.AddDays(-1));

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[AccessTicketHttpHeader.Name] = ticket;

        var accessor = Resolve<IHttpContextAccessor>();
        accessor.HttpContext = httpContext;

        var evaluator = new AccessTicketValidOperationEvaluator(
            accessor,
            signer,
            Resolve<IPublishEndpoint>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AccessTicketValidOperationEvaluator>.Instance);

        // Act: observe publishes on the module bus while the evaluation hits the closed window.
        var bus = Resolve<IBus>();
        RecordingPublishObserver.Clear();
        RecordingPublishObserver.ConnectOnce(bus);

        var service = new ConditionEvaluatorService(
            Resolve<IConditionRepository>(),
            [evaluator]);
        var result = await service.Evaluate(
            new ConditionNode("Id", Operator.AccessTicketValid, JsonDocument.Parse("true").RootElement),
            context);

        // Assert: the evaluation denies AND the publish lands on the bus.
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Condition.AccessWindowClosed");

        for (var i = 0; i < 60 && !RecordingPublishObserver.Messages.OfType<PrincipalExpiredIntegrationEvent>().Any(); i++)
            await Task.Delay(500);

        var published = RecordingPublishObserver.Messages.OfType<PrincipalExpiredIntegrationEvent>().ToList();
        published.Should().ContainSingle().Which.UserId.Should().Be(context.Id);
    }
}

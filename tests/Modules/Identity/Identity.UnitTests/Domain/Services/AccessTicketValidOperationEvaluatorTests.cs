using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Modules.Identity.Domain.Services;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Security;
using AlphaZero.Shared.Infrastructure.Security;
using ErrorOr;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Text.Json;
using Xunit;

namespace AlphaZero.Modules.Identity.UnitTests.Domain.Services;

/// <summary>
/// Step 6 QA gate (issue #34 §4): the AccessTicketValid operator allows a fully valid header
/// ticket and DENIES every failing shape — missing header, malformed token, wrong key, expired
/// token, outside-window token, and a ticket minted for another actor — with NO database. The
/// tests drive the REAL signer (fixed test secret) and route through the evaluator.
/// </summary>
public class AccessTicketValidOperationEvaluatorTests
{
    private const string TestSecret = "unit-test-access-ticket-secret-min-32-chars!!!";
    private const string ForeignSecret = "foreign-key-access-ticket-secret-min-32!!!";

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _enrollmentId = Guid.NewGuid();

    private AuthorizationContext Context() => new()
    {
        Id = _userId,
        TenantId = _tenantId,
        AuthenticationMethod = "TenantUser",
        RequiredPermission = "video:Stream",
        ResourcePath = "video/1",
        ResourceType = "video"
    };

    private static IAccessTicketSigner Signer(string secret) => new AccessTicketSigner(
        Options.Create(new AccessTicketSettings { Secret = secret, TtlSeconds = 7200 }));

    private static IOperationEvaluator Evaluator(IHeaderDictionary? headers)
    {
        var context = new DefaultHttpContext();
        foreach (var (key, value) in headers ?? new HeaderDictionary())
            context.Request.Headers[key] = value;

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);

        return new AccessTicketValidOperationEvaluator(accessor, Signer(TestSecret));
    }

    private static JsonElement TrueValue() => JsonDocument.Parse("true").RootElement;

    private string ValidTicket(int windowEndOffsetDays = 30)
        => Signer(TestSecret).GenerateTicket(_tenantId, _userId, _enrollmentId,
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(windowEndOffsetDays));

    // ------------------------------------------------------------------
    // Allow
    // ------------------------------------------------------------------

    [Fact]
    public async Task Evaluate_ShouldAllow_WhenHeaderCarriesFullyValidTicket()
    {
        var evaluator = Evaluator(new HeaderDictionary
        {
            [AccessTicketHttpHeader.Name] = ValidTicket()
        });

        var result = await evaluator.Evaluate(null!, TrueValue(), Context());

        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task ConditionEvaluatorService_ShouldRouteAccessTicketValid_ToTheOperationEvaluator()
    {
        // End-to-end through the policy tree: { "Property": "Id", "Operator": "AccessTicketValid", "Value": true }
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[AccessTicketHttpHeader.Name] = ValidTicket();
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);

        var evaluator = new AccessTicketValidOperationEvaluator(accessor, Signer(TestSecret));
        var service = new ConditionEvaluatorService(Substitute.For<IConditionRepository>(), [evaluator]);

        var condition = new ConditionNode("Id", Operator.AccessTicketValid, TrueValue());
        var result = await service.Evaluate(condition, Context());

        result.IsError.Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // Deny — fail closed on every non-verify shape (§3.3)
    // ------------------------------------------------------------------

    [Fact]
    public async Task Evaluate_ShouldDeny_WhenHeaderIsMissing()
    {
        var result = await Evaluator(new HeaderDictionary()).Evaluate(null!, TrueValue(), Context());

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Condition.AccessTicketMissing");
    }

    [Fact]
    public async Task Evaluate_ShouldDeny_WhenHttpContextIsUnavailable()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);
        var evaluator = new AccessTicketValidOperationEvaluator(accessor, Signer(TestSecret));

        var result = await evaluator.Evaluate(null!, TrueValue(), Context());

        result.FirstError.Code.Should().Be("Condition.HttpContextUnavailable");
    }

    [Fact]
    public async Task Evaluate_ShouldDeny_WhenTokenIsMalformed()
    {
        var evaluator = Evaluator(new HeaderDictionary
        {
            [AccessTicketHttpHeader.Name] = "this-is.not-a-ticket"
        });

        var result = await evaluator.Evaluate(null!, TrueValue(), Context());

        result.FirstError.Code.Should().Be("Condition.AccessTicketInvalid");
    }

    [Fact]
    public async Task Evaluate_ShouldDeny_WhenTicketIsSignedWithTheWrongKey()
    {
        var ticket = Signer(ForeignSecret).GenerateTicket(_tenantId, _userId, _enrollmentId,
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));

        var evaluator = Evaluator(new HeaderDictionary { [AccessTicketHttpHeader.Name] = ticket });

        var result = await evaluator.Evaluate(null!, TrueValue(), Context());

        result.FirstError.Code.Should().Be("Condition.AccessTicketInvalid");
        result.FirstError.Description.Should().Contain("Signature mismatch");
    }

    [Fact]
    public async Task Evaluate_ShouldDeny_WhenTicketHasExpired()
    {
        var ticket = Signer(TestSecret).GenerateTicket(_tenantId, _userId, _enrollmentId,
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30),
            ttl: TimeSpan.FromSeconds(1));
        await Task.Delay(TimeSpan.FromSeconds(2.2));

        var evaluator = Evaluator(new HeaderDictionary { [AccessTicketHttpHeader.Name] = ticket });

        var result = await evaluator.Evaluate(null!, TrueValue(), Context());

        result.FirstError.Code.Should().Be("Condition.AccessTicketInvalid");
        result.FirstError.Description.Should().Contain("expired");
    }

    [Fact]
    public async Task Evaluate_ShouldDeny_WhenAccessWindowHasClosed()
    {
        var ticket = Signer(TestSecret).GenerateTicket(_tenantId, _userId, _enrollmentId,
            DateTimeOffset.UtcNow.AddDays(-90), DateTimeOffset.UtcNow.AddDays(-1)); // window elapsed

        var evaluator = Evaluator(new HeaderDictionary { [AccessTicketHttpHeader.Name] = ticket });

        var result = await evaluator.Evaluate(null!, TrueValue(), Context());

        result.FirstError.Code.Should().Be("Condition.AccessWindowClosed");
    }

    [Fact]
    public async Task Evaluate_ShouldDeny_WhenTicketWasMintedForAnotherActor()
    {
        var ticket = Signer(TestSecret).GenerateTicket(_tenantId, Guid.NewGuid(), _enrollmentId,
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));

        var evaluator = Evaluator(new HeaderDictionary { [AccessTicketHttpHeader.Name] = ticket });

        var result = await evaluator.Evaluate(null!, TrueValue(), Context());

        result.FirstError.Code.Should().Be("Condition.AccessTicketForeign");
    }
}

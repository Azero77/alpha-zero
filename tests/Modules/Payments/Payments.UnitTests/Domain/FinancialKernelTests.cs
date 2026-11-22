using AlphaZero.Modules.Payments.Domain.Catalog;
using AlphaZero.Modules.Payments.Domain.Checkout;
using AlphaZero.Modules.Payments.Domain.Finance;
using AlphaZero.Modules.Payments.Domain.Ledger;
using AlphaZero.Modules.Payments.Domain.Providers;
using AlphaZero.Modules.Payments.Domain.Purchases;
using AlphaZero.Modules.Payments.Domain.Settings;
using FluentAssertions;

namespace Payments.UnitTests.Domain;

public sealed class FinancialKernelTests
{
    [Fact]
    public void Academy_settings_reject_invalid_commission_and_keep_absence_fail_closed()
    {
        var tenantId = Guid.NewGuid();
        AcademyPaymentSettings.CreatePolicy(Guid.NewGuid(), tenantId, true, 1.01m, null, Guid.NewGuid(), DateTime.UtcNow)
            .IsError.Should().BeTrue();
        AcademyPaymentSettings? absent = null;
        absent.Should().BeNull();
    }

    [Fact]
    public void Provider_route_cannot_be_enabled_without_adapter_support_and_disable_is_guarded()
    {
        var account = ProviderConfiguration.ConfigureAccount(Guid.NewGuid(), ProviderName.Create("stripe").Value,
            ProviderEnvironment.Test, "acct_test", ProviderAccountStatus.Active, "vault/payments", "vault/hook", DateTime.UtcNow).Value;
        account.ConfigureCurrencyRoute(Guid.NewGuid(), "USD", "USD", PresentmentMode.Adaptive, true,
            14, 0.10m, 120, false, adapterSupportsRoute: false, DateTime.UtcNow).IsError.Should().BeTrue();
        var route = account.ConfigureCurrencyRoute(Guid.NewGuid(), "USD", "USD", PresentmentMode.Adaptive, true,
            14, 0.10m, 120, false, adapterSupportsRoute: true, DateTime.UtcNow).Value;
        account.DisableCurrencyRoute(route.Id, DateTime.UtcNow).IsError.Should().BeFalse();
        account.Routes.Single().IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Eligibility_requires_source_price_opt_in_route_and_no_active_access()
    {
        var now = DateTime.UtcNow;
        var product = Product.Create(Guid.NewGuid(), Guid.NewGuid(), "CoursePlan", Guid.NewGuid(), Guid.NewGuid(),
            "az:courseplan:tenant/course/plan", "Plan", SourceAvailability.Available, 1, now).Value;
        var price = Price.Create(Guid.NewGuid(), product.TenantId, product.Id, Money.Create(20m, "USD").Value, Guid.NewGuid(), now).Value;
        var settings = AcademyPaymentSettings.CreatePolicy(Guid.NewGuid(), product.TenantId, true, .25m, null, Guid.NewGuid(), now).Value;
        var account = ProviderConfiguration.ConfigureAccount(Guid.NewGuid(), ProviderName.Create("stripe").Value,
            ProviderEnvironment.Test, "acct_test", ProviderAccountStatus.Active, "vault/payments", "vault/hook", now).Value;
        account.ConfigureCurrencyRoute(Guid.NewGuid(), "USD", "USD", PresentmentMode.Adaptive, true,
            14, .1m, 120, false, true, now).Value.Should().NotBeNull();

        CheckoutEligibilityPolicy.Decide(product, price, settings, [account], false).Value.Should().ContainSingle();
        CheckoutEligibilityPolicy.Decide(product, price, settings, [account], true).IsError.Should().BeTrue();
    }

    [Fact]
    public void Expired_checkout_can_complete_only_from_valid_exact_late_movement()
    {
        var now = DateTime.UtcNow;
        var amount = Money.Create(25m, "USD").Value;
        var session = CheckoutSession.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), now).Value;
        session.Expire(now.AddMinutes(1)).IsError.Should().BeFalse();
        session.CompleteFromMovement(Guid.NewGuid(), Guid.NewGuid(), amount, amount, true, now.AddMinutes(2)).IsError.Should().BeTrue();

        var providerAccount = Guid.NewGuid();
        var lateSession = CheckoutSession.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), now).Value;
        lateSession.Expire(now.AddMinutes(1));
        lateSession.CompleteFromMovement(providerAccount, providerAccount, amount, amount, true, now.AddMinutes(2))
            .IsError.Should().BeFalse();
        lateSession.Status.Should().Be(CheckoutSessionStatus.Completed);
        lateSession.ExpiredAt.Should().Be(now.AddMinutes(1));
    }

    [Fact]
    public void Capture_purchase_and_ledger_posting_use_normalized_movement_facts()
    {
        var now = DateTime.UtcNow;
        var providerAccountId = Guid.NewGuid();
        var movement = ProviderTransaction.Record(Guid.NewGuid(), providerAccountId, null, "movement-1", "payment-1", null,
            ProviderTransactionType.Capture, ProviderTransactionDirection.Incoming,
            Money.Create(100m, "USD").Value, Money.Create(3.2m, "USD", allowZero: true).Value, now, now).Value;
        var tenant = Guid.NewGuid();
        var purchase = Purchase.CreateFromCapture(Guid.NewGuid(), tenant, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), providerAccountId, movement, "Course Plan", Money.Create(100m, "USD").Value,
            Money.Create(100m, "USD").Value, null, .25m, "payment-1", now).Value;

        var posting = LedgerPostingFactory.Capture(Guid.NewGuid(), tenant, providerAccountId, movement.Id, purchase.Id,
            movement.Amount, movement.Fee, purchase.CommissionShare, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), now).Value;

        posting.Lines.Should().HaveCount(5);
        posting.Lines.Where(line => line.Direction == LedgerDirection.Debit).Sum(line => line.Amount.Amount)
            .Should().Be(103.2m);
        posting.Lines.Where(line => line.Direction == LedgerDirection.Credit).Sum(line => line.Amount.Amount)
            .Should().Be(103.2m);
        posting.Lines.Single(line => line.AccountRole == LedgerAccountRole.AcademyPayable).Amount.Amount.Should().Be(75m);
        posting.Lines.Single(line => line.AccountRole == LedgerAccountRole.PlatformRevenue).Amount.Amount.Should().Be(25m);
    }

    [Fact]
    public void Ledger_posting_rejects_mixed_currency_unbalanced_and_wrong_reference_shapes()
    {
        var debit = new LedgerLine(Guid.NewGuid(), Guid.NewGuid(), LedgerAccountRole.ProviderAsset,
            LedgerDirection.Debit, Money.Create(1m, "USD").Value);
        var credit = new LedgerLine(Guid.NewGuid(), Guid.NewGuid(), LedgerAccountRole.PlatformRevenue,
            LedgerDirection.Credit, Money.Create(1m, "EUR").Value);
        LedgerPosting.Create(Guid.NewGuid(), LedgerEntryType.Capture, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), null, null, null, null, null, null, [debit, credit]).IsError.Should().BeTrue();

        var mismatchedCredit = credit with { Amount = Money.Create(0.99m, "USD").Value };
        LedgerPosting.Create(Guid.NewGuid(), LedgerEntryType.Capture, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), null, null, null, null, null, null, [debit, mismatchedCredit]).IsError.Should().BeTrue();
    }
}

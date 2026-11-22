using AlphaZero.Modules.Payments.Application.Checkout;
using AlphaZero.Modules.Payments.Domain.Catalog;
using AlphaZero.Modules.Payments.Domain.Checkout;
using AlphaZero.Modules.Payments.Domain.Ledger;
using AlphaZero.Modules.Payments.Domain.Providers;
using AlphaZero.Modules.Payments.Domain.Settings;
using ErrorOr;
using FluentAssertions;

namespace Payments.UnitTests.Application;

public sealed class CheckoutApplicationTests
{
    [Fact]
    public async Task Available_methods_fail_closed_when_Courses_cannot_answer()
    {
        var fixture = new CheckoutFixture();
        var handler = new GetAvailableCheckoutMethodsHandler(fixture.Repository,
            new StubCourseAccessReader(Error.Conflict("Checkout.EnrollmentCheckUnavailable", "Unavailable")));

        var result = await handler.Handle(fixture.Query, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Checkout.EnrollmentCheckUnavailable");
    }

    [Fact]
    public async Task Available_methods_reject_active_enrollment_and_return_compatible_route_otherwise()
    {
        var fixture = new CheckoutFixture();
        var enrolled = await new GetAvailableCheckoutMethodsHandler(fixture.Repository,
                new StubCourseAccessReader(true))
            .Handle(fixture.Query, CancellationToken.None);
        var eligible = await new GetAvailableCheckoutMethodsHandler(fixture.Repository,
                new StubCourseAccessReader(false))
            .Handle(fixture.Query, CancellationToken.None);

        enrolled.FirstError.Code.Should().Be("Checkout.AlreadyEnrolled");
        eligible.Value.Methods.Should().ContainSingle()
            .Which.ProviderCurrencyRouteId.Should().Be(fixture.RouteId);
    }

    private sealed class CheckoutFixture
    {
        public CheckoutFixture()
        {
            var now = DateTime.UtcNow;
            Product = AlphaZero.Modules.Payments.Domain.Catalog.Product.Create(Guid.NewGuid(), Guid.NewGuid(),
                "CoursePlan", Guid.NewGuid(), Guid.NewGuid(), "az:course:tenant:course/id/plan/id", "Plan",
                SourceAvailability.Available, 1, now).Value;
            Price = AlphaZero.Modules.Payments.Domain.Catalog.Price.Create(Guid.NewGuid(), Product.TenantId,
                Product.Id, Money.Create(20m, "USD").Value, Guid.NewGuid(), now).Value;
            Settings = AcademyPaymentSettings.CreatePolicy(Guid.NewGuid(), Product.TenantId, true, .25m, null,
                Guid.NewGuid(), now).Value;
            var provider = ProviderConfiguration.ConfigureAccount(Guid.NewGuid(), ProviderName.Create("stripe").Value,
                ProviderEnvironment.Test, "acct_test", ProviderAccountStatus.Active, "credential", "webhook", now).Value;
            RouteId = Guid.NewGuid();
            provider.ConfigureCurrencyRoute(RouteId, "USD", "USD", PresentmentMode.Exact, true, 0, 0, 0,
                false, true, now);
            Repository = new StubCheckoutRepository(Product, Price, Settings, [provider]);
            Query = new GetAvailableCheckoutMethods(Product.TenantId, Guid.NewGuid(), Product.Id, Price.Id);
        }

        public Product Product { get; }
        public Price Price { get; }
        public AcademyPaymentSettings Settings { get; }
        public Guid RouteId { get; }
        public StubCheckoutRepository Repository { get; }
        public GetAvailableCheckoutMethods Query { get; }
    }

    private sealed class StubCourseAccessReader(ErrorOr<bool> result) : ICourseAccessReader
    {
        public Task<ErrorOr<bool>> HasActiveEnrollmentAsync(Guid tenantId, Guid userId, string targetResourceArn,
            CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class StubCheckoutRepository(Product product, Price price, AcademyPaymentSettings settings,
        IReadOnlyList<ProviderConfiguration> providers) : ICheckoutRepository
    {
        public Task<Product?> GetProductAsync(Guid tenantId, Guid productId, CancellationToken cancellationToken) =>
            Task.FromResult<Product?>(product);

        public Task<Price?> GetActivePriceAsync(Guid tenantId, Guid productId, Guid priceId,
            CancellationToken cancellationToken) => Task.FromResult<Price?>(price);

        public Task<AcademyPaymentSettings?> GetSettingsAsync(Guid tenantId,
            CancellationToken cancellationToken) => Task.FromResult<AcademyPaymentSettings?>(settings);

        public Task<IReadOnlyList<ProviderConfiguration>> GetProviderConfigurationsAsync(
            CancellationToken cancellationToken) => Task.FromResult(providers);

        public Task<CheckoutSession?> GetPendingSessionAsync(Guid userId, Guid productId,
            CancellationToken cancellationToken) => Task.FromResult<CheckoutSession?>(null);

        public Task AddSessionAsync(CheckoutSession session, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task UpdateSessionAsync(CheckoutSession session, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}

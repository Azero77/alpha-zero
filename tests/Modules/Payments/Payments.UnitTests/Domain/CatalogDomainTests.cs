using AlphaZero.Modules.Payments.Domain.Catalog;
using AlphaZero.Modules.Payments.Domain.Ledger;
using FluentAssertions;

namespace Payments.UnitTests.Domain;

public sealed class CatalogDomainTests
{
    [Fact]
    public void Product_applies_only_newer_mirror_versions_and_preserves_source_truth()
    {
        var productId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var product = Product.Reconstitute(productId, tenantId, "CoursePlan", itemId, courseId,
            "az:courseplan:tenant/course/plan", "Original", SourceAvailability.Unavailable, 4, now, null);

        product.ApplyMirrorUpdate(4, "Stale", SourceAvailability.Available, "az:courseplan:tenant/course/plan", now.AddMinutes(1))
            .Value.Should().BeFalse();
        product.ApplyMirrorUpdate(5, "Current", SourceAvailability.Available, "az:courseplan:tenant/course/plan", now.AddMinutes(2))
            .Value.Should().BeTrue();

        product.Title.Should().Be("Current");
        product.SourceAvailability.Should().Be(SourceAvailability.Available);
        product.SourceVersion.Should().Be(5);
    }

    [Fact]
    public void Price_is_archived_without_mutating_its_amount_or_currency()
    {
        var price = Price.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Money.Create(19.99m, "USD").Value,
            Guid.NewGuid(), DateTime.UtcNow).Value;

        price.Archive(DateTime.UtcNow.AddMinutes(1)).IsError.Should().BeFalse();
        price.IsActive.Should().BeFalse();
        price.ArchivedAt.Should().NotBeNull();
        price.Amount.Amount.Should().Be(19.99m);
        price.Amount.Currency.Should().Be("USD");
        price.Archive(DateTime.UtcNow.AddMinutes(2)).IsError.Should().BeTrue();
    }
}

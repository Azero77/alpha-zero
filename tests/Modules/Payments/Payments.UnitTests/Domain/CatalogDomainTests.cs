using AlphaZero.Modules.Payments.Domain.Catalog;
using AlphaZero.Modules.Payments.Domain.Ledger;
using FluentAssertions;

namespace Payments.UnitTests.Domain;

public sealed class CatalogDomainTests
{
    [Fact]
    public void Product_creates_successfully_without_parent_container_for_generic_item_types()
    {
        var productId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var product = Product.Create(productId, tenantId, "StandaloneLicense", itemId,
            null, "az:license:tenant/license/1", "License 1", SourceAvailability.Available, 1, now);

        product.IsError.Should().BeFalse();
        product.Value.ParentContainerId.Should().BeNull();
        product.Value.ItemType.Should().Be("StandaloneLicense");
    }

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
    public void Product_rejects_target_identity_changes_while_unavailable()
    {
        var product = Product.Create(Guid.NewGuid(), Guid.NewGuid(), "CoursePlan", Guid.NewGuid(),
            Guid.NewGuid(), "az:course:tenant:course/one/plan/one", "Plan", SourceAvailability.Unavailable,
            1, DateTime.UtcNow).Value;

        var result = product.ApplyMirrorUpdate(2, "Plan", SourceAvailability.Unavailable,
            "az:course:tenant:course/two/plan/two", DateTime.UtcNow);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Product.IdentityChanged");
        result.FirstError.Description.Should().Be("A source item cannot change its target resource identity.");
        product.SourceVersion.Should().Be(1);
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

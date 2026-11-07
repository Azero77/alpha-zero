using AlphaZero.Modules.Library.Application.AccessCodes.GenerateBatch;
using FluentAssertions;

namespace Library.UnitTests.Application.AccessCodes;

public class GenerateBatchCommandValidatorTests
{
    private readonly GenerateBatchCommandValidator _validator = new();

    private static GenerateBatchCommand Command(Dictionary<string, object> metadata)
        => new(
            LibraryId: Guid.NewGuid(),
            Quantity: 5,
            StrategyId: "enroll-course",
            TargetResourceArn: "az:course:tenant:course/101",
            FaceValue: 50m,
            Metadata: metadata);

    [Fact]
    public async Task Validate_PlanKeyWithGuid_ShouldPass()
    {
        var command = Command(new() { ["plan"] = Guid.NewGuid().ToString() });

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_LegacyPlanIdAliasWithGuid_ShouldPass()
    {
        var command = Command(new() { ["planId"] = Guid.NewGuid().ToString() });

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_PlanKeyWithActualGuidValue_ShouldPass()
    {
        // The API layer deserializes metadata values as strings, but a boxed Guid
        // (or any value whose string form is a GUID) must stay acceptable.
        var command = Command(new() { ["plan"] = Guid.NewGuid() });

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_PlanKeyWithNonGuid_ShouldFailWithInvalidMetadata()
    {
        var command = Command(new() { ["plan"] = "gold-tier" });

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "Library.Batch.InvalidMetadata");
    }

    [Fact]
    public async Task Validate_LegacyPlanIdAliasWithNonGuid_ShouldFailWithInvalidMetadata()
    {
        var command = Command(new() { ["planId"] = "" });

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "Library.Batch.InvalidMetadata");
    }

    [Fact]
    public async Task Validate_PlanKeyWithWhitespace_ShouldFailWithInvalidMetadata()
    {
        var command = Command(new() { ["plan"] = "  " });

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "Library.Batch.InvalidMetadata");
    }

    [Fact]
    public async Task Validate_DifferentlyCasedPlanKey_ShouldPass()
    {
        // The Courses provisioning contract reads 'plan'/'planId' case-sensitively,
        // so differently cased keys are arbitrary metadata — not plan references.
        var command = Command(new() { ["PLAN"] = "gold-tier" });

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_UnrelatedMetadataKeys_ShouldPass()
    {
        // Metadata is a per-resource-type provisioning section, not plan-only;
        // unknown keys stay unvalidated (Courses interprets its own contract).
        var command = Command(new() { ["section"] = "vip", ["note"] = 42 });

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_NullMetadata_ShouldPass()
    {
        // Legacy callers construct the command without metadata.
        var command = new GenerateBatchCommand(
            LibraryId: Guid.NewGuid(),
            Quantity: 1,
            StrategyId: "enroll-course",
            TargetResourceArn: "az:course:tenant:course/101",
            FaceValue: 50m,
            Metadata: null!);

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }
}

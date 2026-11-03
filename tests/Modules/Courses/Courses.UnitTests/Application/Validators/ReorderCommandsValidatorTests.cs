using AlphaZero.Modules.Courses.Application.Courses.Commands.Reorder;
using FluentAssertions;
using FluentValidation;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Validators;

public class ReorderCommandsValidatorTests
{
    private readonly ReorderSectionsCommandValidator _sectionsValidator = new();
    private readonly ReorderItemsCommandValidator _itemsValidator = new();

    [Fact]
    public async Task ReorderSections_Should_Pass_WhenIdSetIsValid()
    {
        var command = new ReorderSectionsCommand(Guid.NewGuid(), [Guid.NewGuid(), Guid.NewGuid()]);
        var result = await _sectionsValidator.ValidateAsync(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ReorderSections_Should_Fail_WhenIdSetContainsDuplicates()
    {
        var id = Guid.NewGuid();
        var command = new ReorderSectionsCommand(Guid.NewGuid(), [id, id]);
        var result = await _sectionsValidator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(f => f.ErrorMessage.Contains("duplicate"));
    }

    [Fact]
    public async Task ReorderSections_Should_Fail_WhenIdSetContainsEmptyId()
    {
        var command = new ReorderSectionsCommand(Guid.NewGuid(), [Guid.NewGuid(), Guid.Empty]);
        var result = await _sectionsValidator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(f => f.ErrorMessage.Contains("empty ids"));
    }

    [Fact]
    public async Task ReorderItems_Should_Fail_WhenIdSetContainsDuplicates()
    {
        var id = Guid.NewGuid();
        var command = new ReorderItemsCommand(Guid.NewGuid(), Guid.NewGuid(), [id, id]);
        var result = await _itemsValidator.ValidateAsync(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(f => f.ErrorMessage.Contains("duplicate"));
    }
}

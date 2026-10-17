using System.Text.Json;
using AlphaZero.Modules.Courses.Application.Courses.Commands.Assets;
using AlphaZero.Modules.Courses.Application.Courses.Commands.Playback;
using AlphaZero.Modules.Courses.Application.Courses.Commands.State;
using AlphaZero.Modules.Courses.Application.Courses.Commands.SyncResourceMetadata;
using AlphaZero.Modules.Courses.Application.Courses.Commands.UpsertOverview;
using FluentAssertions;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Application.Validators;

public abstract class ValidatorTestBase
{
    protected static async Task<FluentValidation.Results.ValidationResult> Validate<T>(
        FluentValidation.IValidator<T> validator, T command)
        => await validator.ValidateAsync(command);
}

public class CourseStateCommandValidatorTests : ValidatorTestBase
{
    [Fact]
    public async Task Submit_Should_Fail_WhenCourseIdEmpty()
        => (await Validate(new SubmitCourseForReviewCommandValidator(), new SubmitCourseForReviewCommand(Guid.Empty)))
            .IsValid.Should().BeFalse();

    [Fact]
    public async Task Approve_Should_Fail_WhenCourseIdEmpty()
        => (await Validate(new ApproveCourseCommandValidator(), new ApproveCourseCommand(Guid.Empty)))
            .IsValid.Should().BeFalse();

    [Fact]
    public async Task Publish_Should_Fail_WhenCourseIdEmpty()
        => (await Validate(new PublishCourseCommandValidator(), new PublishCourseCommand(Guid.Empty)))
            .IsValid.Should().BeFalse();

    [Fact]
    public async Task Archive_Should_Fail_WhenCourseIdEmpty()
        => (await Validate(new ArchiveCourseCommandValidator(), new ArchiveCourseCommand(Guid.Empty)))
            .IsValid.Should().BeFalse();

    [Fact]
    public async Task Reject_Should_Fail_WhenCourseIdEmptyOrReasonEmpty()
    {
        (await Validate(new RejectCourseCommandValidator(), new RejectCourseCommand(Guid.Empty, "reason")))
            .IsValid.Should().BeFalse();
        (await Validate(new RejectCourseCommandValidator(), new RejectCourseCommand(Guid.NewGuid(), "")))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Submit_Should_Pass_WhenValid()
        => (await Validate(new SubmitCourseForReviewCommandValidator(), new SubmitCourseForReviewCommand(Guid.NewGuid())))
            .IsValid.Should().BeTrue();

    [Fact]
    public async Task Reject_Should_Pass_WhenValid()
        => (await Validate(new RejectCourseCommandValidator(), new RejectCourseCommand(Guid.NewGuid(), "bad content")))
            .IsValid.Should().BeTrue();
}

public class AssetCommandValidatorTests : ValidatorTestBase
{
    [Fact]
    public async Task Assign_Should_Fail_WhenRequiredFieldsMissing()
    {
        var validator = new AssignAssetToCurriculumCommandValidator();
        (await Validate(validator, new AssignAssetToCurriculumCommand(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), "T")))
            .IsValid.Should().BeFalse();
        (await Validate(validator, new AssignAssetToCurriculumCommand(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), "T")))
            .IsValid.Should().BeFalse();
        (await Validate(validator, new AssignAssetToCurriculumCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, "T")))
            .IsValid.Should().BeFalse();
        (await Validate(validator, new AssignAssetToCurriculumCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "")))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Assign_Should_Fail_WhenTitleExceedsMaxLength()
        => (await Validate(new AssignAssetToCurriculumCommandValidator(),
                new AssignAssetToCurriculumCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string('a', 257))))
            .IsValid.Should().BeFalse();

    [Fact]
    public async Task Assign_Should_Pass_WhenValid()
        => (await Validate(new AssignAssetToCurriculumCommandValidator(),
                new AssignAssetToCurriculumCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Lesson 1")))
            .IsValid.Should().BeTrue();

    [Fact]
    public async Task Unassign_Should_Fail_WhenIdsEmpty()
    {
        (await Validate(new UnassignAssetFromCurriculumCommandValidator(), new UnassignAssetFromCurriculumCommand(Guid.Empty, Guid.NewGuid())))
            .IsValid.Should().BeFalse();
        (await Validate(new UnassignAssetFromCurriculumCommandValidator(), new UnassignAssetFromCurriculumCommand(Guid.NewGuid(), Guid.Empty)))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Dismiss_Should_Fail_WhenIdsEmpty()
    {
        (await Validate(new DismissAssetCommandValidator(), new DismissAssetCommand(Guid.Empty, Guid.NewGuid())))
            .IsValid.Should().BeFalse();
        (await Validate(new DismissAssetCommandValidator(), new DismissAssetCommand(Guid.NewGuid(), Guid.Empty)))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task MarkAvailable_Should_Fail_WhenAssetIdEmpty()
        => (await Validate(new MarkCourseAssetAvailableCommandValidator(), new MarkCourseAssetAvailableCommand(Guid.Empty)))
            .IsValid.Should().BeFalse();

    [Fact]
    public async Task MarkFailed_Should_Fail_WhenAssetIdEmptyOrReasonEmpty()
    {
        (await Validate(new MarkCourseAssetFailedCommandValidator(), new MarkCourseAssetFailedCommand(Guid.Empty, "oops")))
            .IsValid.Should().BeFalse();
        (await Validate(new MarkCourseAssetFailedCommandValidator(), new MarkCourseAssetFailedCommand(Guid.NewGuid(), "")))
            .IsValid.Should().BeFalse();
    }
}

public class UpsertCourseOverviewCommandValidatorTests : ValidatorTestBase
{
    [Fact]
    public async Task Should_Fail_WhenCourseIdEmpty()
        => (await Validate(new UpsertCourseOverviewCommandValidator(),
                new UpsertCourseOverviewCommand(Guid.Empty, "{}", null, null)))
            .IsValid.Should().BeFalse();

    [Fact]
    public async Task Should_Fail_WhenDescriptionContentEmpty()
        => (await Validate(new UpsertCourseOverviewCommandValidator(),
                new UpsertCourseOverviewCommand(Guid.NewGuid(), "", null, null)))
            .IsValid.Should().BeFalse();

    [Fact]
    public async Task Should_Pass_WhenValid()
        => (await Validate(new UpsertCourseOverviewCommandValidator(),
                new UpsertCourseOverviewCommand(Guid.NewGuid(), "{\"type\":\"doc\"}", null, null)))
            .IsValid.Should().BeTrue();
}

public class IssuePlaybackTicketCommandValidatorTests : ValidatorTestBase
{
    [Fact]
    public async Task Should_Fail_WhenCourseIdEmpty()
        => (await Validate(new IssuePlaybackTicketCommandValidator(), new IssuePlaybackTicketCommand(Guid.Empty, Guid.NewGuid())))
            .IsValid.Should().BeFalse();

    [Fact]
    public async Task Should_Fail_WhenCurriculumResourceIdEmpty()
        => (await Validate(new IssuePlaybackTicketCommandValidator(), new IssuePlaybackTicketCommand(Guid.NewGuid(), Guid.Empty)))
            .IsValid.Should().BeFalse();

    [Fact]
    public async Task Should_Pass_WhenValid()
        => (await Validate(new IssuePlaybackTicketCommandValidator(), new IssuePlaybackTicketCommand(Guid.NewGuid(), Guid.NewGuid())))
            .IsValid.Should().BeTrue();
}

public class SyncResourceMetadataCommandValidatorTests : ValidatorTestBase
{
    [Fact]
    public async Task Should_Fail_WhenResourceIdEmpty()
        => (await Validate(new SyncResourceMetadataCommandValidator(),
                new SyncResourceMetadataCommand(Guid.Empty, JsonDocument.Parse("{}").RootElement)))
            .IsValid.Should().BeFalse();

    [Fact]
    public async Task Should_Fail_WhenMetadataIsNotAJsonObject()
        => (await Validate(new SyncResourceMetadataCommandValidator(),
                new SyncResourceMetadataCommand(Guid.NewGuid(), default)))
            .IsValid.Should().BeFalse();

    [Fact]
    public async Task Should_Pass_WhenMetadataIsJsonObject()
        => (await Validate(new SyncResourceMetadataCommandValidator(),
                new SyncResourceMetadataCommand(Guid.NewGuid(), JsonDocument.Parse("{\"Title\":\"t\"}").RootElement)))
            .IsValid.Should().BeTrue();
}

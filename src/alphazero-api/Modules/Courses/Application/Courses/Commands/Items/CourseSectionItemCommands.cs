using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Application;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AlphaZero.Modules.Courses.Application.Courses.Commands.Items;

public record CurriculumItemDto(
    Guid Id,
    Guid SectionId,
    string Title,
    string MainType,
    int Order,
    int BitIndex,
    JsonElement Content);

public record CreateCourseSectionItemCommand(
    Guid CourseId,
    Guid SectionId,
    string Title,
    string MainType,
    JsonElement Content) : ICommand<CurriculumItemDto>;

public record UpdateCourseSectionItemCommand(
    Guid CourseId,
    Guid SectionId,
    Guid ItemId,
    string Title,
    string MainType,
    JsonElement Content) : ICommand<CurriculumItemDto>;

public record DeleteCourseSectionItemCommand(
    Guid CourseId,
    Guid SectionId,
    Guid ItemId) : ICommand<Success>;

public class CreateCourseSectionItemCommandValidator : AbstractValidator<CreateCourseSectionItemCommand>
{
    public CreateCourseSectionItemCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.SectionId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(256);
        RuleFor(x => x.MainType).NotEmpty().Must(CurriculumItem.IsValidMainType)
            .WithMessage($"MainType must be one of: {string.Join(", ", CurriculumItem.ValidMainTypes)}.");
    }
}

public class UpdateCourseSectionItemCommandValidator : AbstractValidator<UpdateCourseSectionItemCommand>
{
    public UpdateCourseSectionItemCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.SectionId).NotEmpty();
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(256);
        RuleFor(x => x.MainType).NotEmpty().Must(CurriculumItem.IsValidMainType)
            .WithMessage($"MainType must be one of: {string.Join(", ", CurriculumItem.ValidMainTypes)}.");
    }
}

public class DeleteCourseSectionItemCommandValidator : AbstractValidator<DeleteCourseSectionItemCommand>
{
    public DeleteCourseSectionItemCommandValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.SectionId).NotEmpty();
        RuleFor(x => x.ItemId).NotEmpty();
    }
}

public sealed class CourseSectionItemHandlers :
    IRequestHandler<CreateCourseSectionItemCommand, ErrorOr<CurriculumItemDto>>,
    IRequestHandler<UpdateCourseSectionItemCommand, ErrorOr<CurriculumItemDto>>,
    IRequestHandler<DeleteCourseSectionItemCommand, ErrorOr<Success>>
{
    private readonly ICourseRepository _courseRepository;
    private readonly ILogger<CourseSectionItemHandlers> _logger;

    public CourseSectionItemHandlers(
        ICourseRepository courseRepository,
        ILogger<CourseSectionItemHandlers> logger)
    {
        _courseRepository = courseRepository;
        _logger = logger;
    }

    public async Task<ErrorOr<CurriculumItemDto>> Handle(
        CreateCourseSectionItemCommand request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.GetByIdWithSectionsAndAssetsAsync(request.CourseId, cancellationToken);
        if (course is null)
            return Error.NotFound("Course.NotFound", "Course not found.");

        var itemResult = course.AddSectionItem(
            request.SectionId,
            request.Title,
            request.MainType,
            request.Content);

        if (itemResult.IsError)
            return itemResult.Errors;

        _courseRepository.Update(course);
        _logger.LogInformation("Added curriculum item {ItemId} to course {CourseId}, section {SectionId}.",
            itemResult.Value.Id, request.CourseId, request.SectionId);

        var item = itemResult.Value;
        return new CurriculumItemDto(
            item.Id,
            item.SectionId,
            item.Title,
            item.MainType,
            item.Order,
            item.BitIndex,
            item.Content);
    }

    public async Task<ErrorOr<CurriculumItemDto>> Handle(
        UpdateCourseSectionItemCommand request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.GetByIdWithSectionsAndAssetsAsync(request.CourseId, cancellationToken);
        if (course is null)
            return Error.NotFound("Course.NotFound", "Course not found.");

        var itemResult = course.UpdateSectionItem(
            request.SectionId,
            request.ItemId,
            request.Title,
            request.MainType,
            request.Content);

        if (itemResult.IsError)
            return itemResult.Errors;

        _courseRepository.Update(course);
        _logger.LogInformation("Updated curriculum item {ItemId} in course {CourseId}.",
            request.ItemId, request.CourseId);

        var item = itemResult.Value;
        return new CurriculumItemDto(
            item.Id,
            item.SectionId,
            item.Title,
            item.MainType,
            item.Order,
            item.BitIndex,
            item.Content);
    }

    public async Task<ErrorOr<Success>> Handle(
        DeleteCourseSectionItemCommand request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.GetByIdWithSectionsAndAssetsAsync(request.CourseId, cancellationToken);
        if (course is null)
            return Error.NotFound("Course.NotFound", "Course not found.");

        var deleteResult = course.DeleteSectionItem(request.SectionId, request.ItemId);
        if (deleteResult.IsError)
            return deleteResult.Errors;

        _courseRepository.Update(course);
        _logger.LogInformation("Deleted curriculum item {ItemId} from course {CourseId}.",
            request.ItemId, request.CourseId);

        return Result.Success;
    }
}

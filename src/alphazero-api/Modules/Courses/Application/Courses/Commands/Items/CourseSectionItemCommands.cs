using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Application;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AlphaZero.Modules.Courses.Application.Courses.Commands.Items;

public record CurriculumResourceDto(
    Guid Id,
    int Order,
    string ResourceType,
    Guid? CourseAssetId,          // Layer 2 — the pool asset id (single source of truth)
    double? DurationSeconds = null,
    string? DocumentTitle = null,
    string? DocumentType = null,
    bool? IsPublic = null,
    string? VariantName = null,
    string? DocUri = null,
    string? TextContent = null,
    int? PassingScore = null)
{
    public static CurriculumResourceDto From(CurriculumResource resource) => resource switch
    {
        VideoCurriculumResource v => new(
            v.Id, v.Order, v.ResourceType.ToString(), v.CourseAssetId,
            DurationSeconds: v.DurationSeconds),

        DocumentCurriculumResource d => new(
            d.Id, d.Order, d.ResourceType.ToString(), d.CourseAssetId,
            DocumentTitle: d.Title, DocumentType: d.DocumentType,
            IsPublic: d.IsPublic, VariantName: d.VariantName, DocUri: d.DocUri),

        ArticleCurriculumResource a => new(
            a.Id, a.Order, a.ResourceType.ToString(), a.CourseAssetId,
            TextContent: a.TextContent),

        AssessmentCurriculumResource q => new(
            q.Id, q.Order, q.ResourceType.ToString(), q.CourseAssetId,
            PassingScore: q.PassingScore),

        _ => new(resource.Id, resource.Order, resource.ResourceType.ToString(), resource.CourseAssetId)
    };
}

public record CurriculumItemDto(
    Guid Id,
    Guid SectionId,
    string Title,
    string MainType,
    int Order,
    int BitIndex,
    JsonElement Content,
    List<CurriculumResourceDto>? Resources = null)
{
    private static readonly JsonElement EmptyArrayElement = JsonDocument.Parse("[]").RootElement.Clone();

    public static CurriculumItemDto From(CurriculumItem item, JsonElement? rawContent = null)
    {
        var content = rawContent.HasValue && rawContent.Value.ValueKind != JsonValueKind.Undefined
            ? rawContent.Value.Clone()
            : EmptyArrayElement;

        var resources = item.Resources.Select(CurriculumResourceDto.From).ToList();

        return new CurriculumItemDto(
            item.Id,
            item.SectionId,
            item.Title,
            item.MainResourceType.ToString(),
            item.Order,
            item.BitIndex,
            content,
            resources);
    }
}

public static class CurriculumResourceParser
{
    public static (List<CurriculumResource> Resources, List<string> Warnings) Parse(string mainType, JsonElement content)
    {
        var resources = new List<CurriculumResource>();
        var warnings = new List<string>();
        if (content.ValueKind != JsonValueKind.Array)
            return (resources, warnings);

        int order = 0;
        foreach (var block in content.EnumerateArray())
        {
            if (block.ValueKind != JsonValueKind.Object) continue;

            var type = block.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : mainType;
            Guid? courseAssetId = null;
            if (block.TryGetProperty("courseAssetId", out var caProp) && Guid.TryParse(caProp.GetString(), out var caId))
            {
                courseAssetId = caId;
            }

            var normalizedType = (type ?? mainType).ToLowerInvariant();

            if (!courseAssetId.HasValue)
            {
                // A curriculum resource can only exist as a placement of a CourseAsset pool entry.
                // External ids (videoId / documentId / assessmentId) are Layer-1 identities and must
                // NEVER be ingested here — doing so would violate the CourseAssets foreign key.
                warnings.Add($"Skipped {normalizedType} block at index {order}: missing courseAssetId");
                continue;
            }

            if (normalizedType is "video")
            {
                var duration = block.TryGetProperty("durationSeconds", out var dProp) && dProp.TryGetDouble(out var d) ? d : 0;
                resources.Add(new VideoCurriculumResource(Guid.NewGuid(), order++, courseAssetId.Value, duration));
            }
            else if (normalizedType is "document")
            {
                var title = block.TryGetProperty("title", out var tProp) ? tProp.GetString() ?? "Document" : "Document";
                var docType = block.TryGetProperty("documentType", out var dtProp) ? dtProp.GetString() ?? "pdf" : "pdf";
                var isPublic = !block.TryGetProperty("isPublic", out var pubProp) || pubProp.GetBoolean();
                var variant = block.TryGetProperty("variantName", out var varProp) ? varProp.GetString() : null;

                if (block.TryGetProperty("docUri", out var uriProp) && !string.IsNullOrWhiteSpace(uriProp.GetString()))
                {
                    try
                    {
                        var parsed = DocumentCurriculumResource.ParseDocUri(uriProp.GetString()!);
                        isPublic = parsed.IsPublic;
                        variant = parsed.VariantName;
                    }
                    catch
                    {
                        // Fall back to original properties
                    }
                }

                resources.Add(new DocumentCurriculumResource(Guid.NewGuid(), order++, title, courseAssetId.Value, docType, isPublic, variant));
            }
            else if (normalizedType is "quiz" or "assessment")
            {
                var passingScore = block.TryGetProperty("passingScore", out var pProp) && pProp.TryGetInt32(out var p) ? p : 80;
                resources.Add(new AssessmentCurriculumResource(Guid.NewGuid(), order++, passingScore, courseAssetId.Value));
            }
            else
            {
                // Article / text / inline
                resources.Add(new ArticleCurriculumResource(Guid.NewGuid(), order++, block, courseAssetId));
            }
        }

        return (resources, warnings);
    }
}

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
        var course = await _courseRepository.Query(request.CourseId)
            .WithSection(request.SectionId)
            .WithItems()
            .ExecuteAsync(cancellationToken);
        if (course is null)
            return Error.NotFound("Course.NotFound", "Course not found.");

        var section = course.Sections.FirstOrDefault(s => s.Id == request.SectionId && !s.IsDeleted);
        if (section is null)
            return Error.NotFound("Course.Section", "Section not found.");

        var (resources, parseWarnings) = CurriculumResourceParser.Parse(request.MainType, request.Content);
        LogParseWarnings(parseWarnings, "create");

        var bitIndex = course.AllocateNextBitIndex();
        var mainType = CurriculumItem.ParseMainType(request.MainType);
        var itemResult = section.AddItem(
            Guid.NewGuid(),
            request.Title,
            mainType,
            bitIndex,
            resources);

        if (itemResult.IsError)
            return itemResult.Errors;

        _courseRepository.Update(course);
        _logger.LogInformation("Added curriculum item {ItemId} to course {CourseId}, section {SectionId}.",
            itemResult.Value.Id, request.CourseId, request.SectionId);

        return CurriculumItemDto.From(itemResult.Value, request.Content);
    }

    public async Task<ErrorOr<CurriculumItemDto>> Handle(
        UpdateCourseSectionItemCommand request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.Query(request.CourseId)
            .WithSection(request.SectionId)
            .WithItems()
            .WithResources()
            .ExecuteAsync(cancellationToken);
        if (course is null)
            return Error.NotFound("Course.NotFound", "Course not found.");

        var section = course.Sections.FirstOrDefault(s => s.Id == request.SectionId && !s.IsDeleted);
        if (section is null)
            return Error.NotFound("Course.Section", "Section not found.");

        var item = section.Items.FirstOrDefault(i => i.Id == request.ItemId && !i.IsDeleted);
        if (item is null)
            return Error.NotFound("CurriculumItem.NotFound", "Item not found in section.");

        var (newResources, parseWarnings) = CurriculumResourceParser.Parse(request.MainType, request.Content);
        LogParseWarnings(parseWarnings, "update");
        var mainType = CurriculumItem.ParseMainType(request.MainType);

        var updateResult = section.UpdateItem(request.ItemId, request.Title, mainType, newResources);
        if (updateResult.IsError)
            return updateResult.Errors;

        _courseRepository.Update(course);
        _logger.LogInformation("Updated curriculum item {ItemId} in course {CourseId}.",
            request.ItemId, request.CourseId);

        return CurriculumItemDto.From(item, request.Content);
    }

    public async Task<ErrorOr<Success>> Handle(
        DeleteCourseSectionItemCommand request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.Query(request.CourseId)
            .WithSection(request.SectionId)
            .WithItems()
            .ExecuteAsync(cancellationToken);
        if (course is null)
            return Error.NotFound("Course.NotFound", "Course not found.");

        var section = course.Sections.FirstOrDefault(s => s.Id == request.SectionId && !s.IsDeleted);
        if (section is null)
            return Error.NotFound("Course.Section", "Section not found.");

        var item = section.Items.FirstOrDefault(i => i.Id == request.ItemId && !i.IsDeleted);
        if (item is null)
            return Error.NotFound("CurriculumItem.NotFound", "Item not found.");

        var removeResult = section.RemoveItem(request.ItemId);
        if (removeResult.IsError)
            return removeResult.Errors;

        _courseRepository.Update(course);
        _logger.LogInformation("Deleted curriculum item {ItemId} from course {CourseId}.",
            request.ItemId, request.CourseId);

        return Result.Success;
    }

    private void LogParseWarnings(List<string> warnings, string operation)
    {
        foreach (var warning in warnings)
        {
            _logger.LogWarning("Curriculum resource parse warning ({Operation}): {Warning}",
                operation, warning);
        }
    }
}

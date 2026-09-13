using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Infrastructure.Repositores;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AlphaZero.Modules.Courses.Application.Courses.Commands.SyncResourceMetadata;

public record SyncResourceMetadataCommand(Guid ResourceId, JsonElement Metadata) : ICommand<Success>;

public sealed class SyncResourceMetadataCommandHandler : IRequestHandler<SyncResourceMetadataCommand, ErrorOr<Success>>
{
    private readonly IRepository<CourseAsset> _assetRepository;
    private readonly ILogger<SyncResourceMetadataCommandHandler> _logger;

    public SyncResourceMetadataCommandHandler(
        IRepository<CourseAsset> assetRepository, 
        ILogger<SyncResourceMetadataCommandHandler> logger)
    {
        _assetRepository = assetRepository;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> Handle(SyncResourceMetadataCommand request, CancellationToken ct)
    {
        var asset = await _assetRepository.GetById(request.ResourceId, ct);
        if (asset is null)
        {
            _logger.LogDebug("No course asset found for resource {ResourceId}", request.ResourceId);
            return Result.Success;
        }

        if (request.Metadata.TryGetProperty("Title", out var titleProp) && titleProp.GetString() is string title && !string.IsNullOrWhiteSpace(title))
        {
            asset.UpdateTitle(title);
        }

        if (asset is AssessmentCourseAsset assessment)
        {
            if (request.Metadata.TryGetProperty("Type", out var typeProp) && Enum.TryParse<AssessmentType>(typeProp.GetString(), out var type))
            {
                assessment.UpdateAssessmentInfo(assessment.QuestionsNumber, type);
            }
        }

        _assetRepository.Update(asset);
        _logger.LogInformation("Synchronized metadata for Resource {ResourceId}.", request.ResourceId);

        return Result.Success;
    }
}

using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Shared.Application;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AlphaZero.Modules.Courses.Application.Courses.Commands.SyncResourceMetadata;

public record SyncResourceMetadataCommand(Guid ResourceId, JsonElement Metadata) : ICommand<Success>;

public sealed class SyncResourceMetadataCommandHandler : IRequestHandler<SyncResourceMetadataCommand, ErrorOr<Success>>
{
    private readonly ICourseRepository _courseRepository;
    private readonly ILogger<SyncResourceMetadataCommandHandler> _logger;

    public SyncResourceMetadataCommandHandler(ICourseRepository courseRepository, ILogger<SyncResourceMetadataCommandHandler> logger)
    {
        _courseRepository = courseRepository;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> Handle(SyncResourceMetadataCommand request, CancellationToken ct)
    {
        var courses = await _courseRepository.GetCoursesByResourceIdAsync(request.ResourceId, ct);
        
        if (courses.Count == 0)
        {
            _logger.LogDebug("No courses found linked to resource {ResourceId}", request.ResourceId);
            return Result.Success;
        }

        foreach (var course in courses)
        {
            course.UpdateResourceMetadata(request.ResourceId, request.Metadata);
            _courseRepository.Update(course);
        }

        _logger.LogInformation("Synchronized metadata for Resource {ResourceId} across {Count} courses.", request.ResourceId, courses.Count);

        return Result.Success;
    }
}

using AlphaZero.Modules.VideoUploading.Application.Repositories;
using AlphaZero.Shared.Application;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.VideoUploading.Application.Commands.Update;

public record UpdateVideoInfoCommand(Guid VideoId, string Title, string? Description) : ICommand<Success>;

public sealed class UpdateVideoInfoCommandHandler : IRequestHandler<UpdateVideoInfoCommand, ErrorOr<Success>>
{
    private readonly IVideoRepository _videoRepository;
    private readonly ILogger<UpdateVideoInfoCommandHandler> _logger;

    public UpdateVideoInfoCommandHandler(
        IVideoRepository videoRepository,
        ILogger<UpdateVideoInfoCommandHandler> logger)
    {
        _videoRepository = videoRepository;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> Handle(UpdateVideoInfoCommand request, CancellationToken cancellationToken)
    {
        var video = await _videoRepository.GetByIdAsync(request.VideoId, cancellationToken);
        if (video == null)
        {
            return Error.NotFound("Video.NotFound", $"Video with ID {request.VideoId} was not found.");
        }

        var updateResult = video.UpdateInformation(request.Title, request.Description);
        if (updateResult.IsError)
        {
            return updateResult.Errors;
        }

        _logger.LogInformation("Updating info for video {VideoId}.", video.Id);
        _videoRepository.Update(video);

        return Result.Success;
    }
}

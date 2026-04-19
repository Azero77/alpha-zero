using AlphaZero.Shared.Presentation.Extensions;
using AlphaZero.Modules.VideoUploading.Application.Queries.GetVideo;
using AlphaZero.Modules.VideoUploading.Application.Queries.ListVideos;
using AlphaZero.Modules.VideoUploading.Application.Queries.GetVideoState;
using AlphaZero.Modules.VideoUploading.Application.Commands.Delete;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.API.Shared;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AlphaZero.Modules.VideoUploading.Presentation.Features;

public static class Debug
{
    public record VideoResponse(
        Guid Id,
        string Title,
        string? Description,
        string Status,
        AlphaZero.Modules.VideoUploading.Domain.Models.VideoMetadata Metadata,
        AlphaZero.Modules.VideoUploading.Domain.Models.VideoSpecifications Specifications,
        string SourceKey,
        string? OutputFolder,
        DateTime CreatedOn,
        DateTime? PublishedOn);

    private static VideoResponse MapToResponse(AlphaZero.Modules.VideoUploading.Domain.Models.Video video)
    {
        return new VideoResponse(
            video.Id,
            video.Title,
            video.Description,
            video.Status.ToString(),
            video.Metadata,
            video.Specifications,
            video.SourceKey,    
            video.OutputFolder,
            video.CreatedOn,
            video.PublishedOn);
    }

    public class GetVideosEndpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("api/video-uploading/debug/videos", Handler)
               .WithTags("Video Uploading Debug")
               .AccessControl("video:List", _ => ResourceArn.ForTenant(Guid.Empty));
        }

        private async Task<IResult> Handler(int? page, int? perPage, VideoUploadingModule module)
        {
            var query = new ListVideosQuery(page ?? 1, perPage ?? 10);
            var response = await module.Send<ListVideosQuery, ErrorOr<AlphaZero.Shared.Queries.PagedResult<AlphaZero.Modules.VideoUploading.Domain.Models.Video>>>(query);
            return response.Match(
                res => Results.Ok(new AlphaZero.Shared.Queries.PagedResult<VideoResponse>(
                    res.Items.Select(MapToResponse).ToList(),
                    res.TotalCount,
                    res.CurrentPage,
                    res.PageSize)),
                errors => errors.ToMinimalResult());
        }
    }

    public class GetVideoEndpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("api/video-uploading/debug/videos/{id:guid}", Handler)
               .WithTags("Video Uploading Debug")
               .AccessControl("video:View", ctx => ResourceArn.ForVideo(Guid.Empty, Guid.Parse(ctx.Request.RouteValues["id"]?.ToString() ?? Guid.Empty.ToString())));
        }

        private async Task<IResult> Handler(Guid id, VideoUploadingModule module)
        {
            var query = new GetVideoQuery(id);
            var response = await module.Send<GetVideoQuery, ErrorOr<AlphaZero.Modules.VideoUploading.Domain.Models.Video>>(query);
            return response.Match(
                res => Results.Ok(MapToResponse(res)),
                errors => errors.ToMinimalResult());
        }
    }

    public class GetVideoStateEndpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("api/video-uploading/debug/videos/{id:guid}/state", Handler)
               .WithTags("Video Uploading Debug")
               .AccessControl("video:View", ctx => ResourceArn.ForVideo(Guid.Empty, Guid.Parse(ctx.Request.RouteValues["id"]?.ToString() ?? Guid.Empty.ToString())));
        }

        private async Task<IResult> Handler(Guid id, VideoUploadingModule module)
        {
            var query = new GetVideoStateQuery(id);
            var response = await module.Send<GetVideoStateQuery, ErrorOr<AlphaZero.Modules.VideoUploading.Application.Repositories.VideoStateDto>>(query);
            return response.Match(
                res => Results.Ok(res),
                errors => errors.ToMinimalResult());
        }
    }

    public class DeleteVideoEndpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapDelete("api/video-uploading/debug/videos/{id:guid}", Handler)
               .WithTags("Video Uploading Debug")
               .AccessControl("video:Delete", ctx => ResourceArn.ForVideo(Guid.Empty, Guid.Parse(ctx.Request.RouteValues["id"]?.ToString() ?? Guid.Empty.ToString())));
        }

        private async Task<IResult> Handler(Guid id, VideoUploadingModule module)
        {
            var command = new DeleteVideoCommand(id);
            var response = await module.Send<DeleteVideoCommand, ErrorOr<Deleted>>(command);
            return response.Match(
                res => Results.NoContent(),
                errors => errors.ToMinimalResult());
        }
    }
}

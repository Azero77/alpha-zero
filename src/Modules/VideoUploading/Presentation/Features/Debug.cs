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
using AlphaZero.Shared.Queries;
using AlphaZero.Modules.VideoUploading.Domain.Models;

using Aspire.Shared;

namespace AlphaZero.Modules.VideoUploading.Presentation.Features;

public static class Debug
{
    public record VideoResponse(
        Guid Id,
        string Title,
        string? Description,
        string Status,
        string? ThumbnailUrl,
        string? StreamingUrl,
        AlphaZero.Modules.VideoUploading.Domain.Models.VideoMetadata Metadata,
        AlphaZero.Modules.VideoUploading.Domain.Models.VideoSpecifications Specifications,
        string SourceKey,
        string? OutputFolder,
        DateTime CreatedOn,
        DateTime? PublishedOn);

    private static VideoResponse MapToResponse(AlphaZero.Modules.VideoUploading.Domain.Models.Video video, AWSResources resources)
    {
        var domain = resources.CdnDomain;
        
        string? thumbnailUrl = video.Thumbnail?.ThumbnailUrl;
        if (!string.IsNullOrEmpty(thumbnailUrl) && !thumbnailUrl.StartsWith("http"))
        {
            thumbnailUrl = $"http://{domain}/{thumbnailUrl.TrimStart('/')}";
        }

        string? streamingUrl = null;
        if (video.Status == VideoStatus.Published && !string.IsNullOrEmpty(video.OutputFolder))
        {
            streamingUrl = video.OutputFolder.StartsWith("http") 
                ? video.OutputFolder 
                : $"http://{domain}/{video.OutputFolder.TrimStart('/')}/master.m3u8";
        }

        return new VideoResponse(
            video.Id,
            video.Title,
            video.Description,
            video.Status.ToString(),
            thumbnailUrl,
            streamingUrl,
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
               .AccessControl("video:List", (req, tenantId) => ResourceArn.ForTenant(tenantId));
        }

        private async Task<IResult> Handler(int? page, int? perPage, VideoUploadingModule module, AWSResources resources)
        {
            var query = new ListVideosQuery(page ?? 1, perPage ?? 10);
            var response = await module.Send<ListVideosQuery, ErrorOr<PagedResult<Video>>>(query);
            return response.Match(
                res => Results.Ok(new PagedResult<VideoResponse>(
                    res.Items.Select(v => MapToResponse(v, resources)).ToList(),
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
               .AccessControl("video:View", (ctx, tenantId) => ResourceArn.ForVideo(tenantId, Guid.Parse(ctx.Request.RouteValues["id"]?.ToString() ?? Guid.Empty.ToString())));
        }

        private async Task<IResult> Handler(Guid id, VideoUploadingModule module, AWSResources resources)
        {
            var query = new GetVideoQuery(id);
            var response = await module.Send<GetVideoQuery, ErrorOr<AlphaZero.Modules.VideoUploading.Domain.Models.Video>>(query);
            return response.Match(
                res => Results.Ok(MapToResponse(res, resources)),
                errors => errors.ToMinimalResult());
        }
    }

    public class GetVideoStateEndpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("api/video-uploading/debug/videos/{id:guid}/state", Handler)
               .WithTags("Video Uploading Debug")
               .AccessControl("video:View", (ctx, tenantId) => ResourceArn.ForVideo(tenantId, Guid.Parse(ctx.Request.RouteValues["id"]?.ToString() ?? Guid.Empty.ToString())));
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

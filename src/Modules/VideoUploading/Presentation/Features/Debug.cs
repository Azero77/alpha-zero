using AlphaZero.Shared.Presentation.Extensions;
using AlphaZero.Modules.VideoUploading.Application.Queries.GetVideo;
using AlphaZero.Modules.VideoUploading.Application.Queries.ListVideos;
using AlphaZero.Modules.VideoUploading.Application.Queries.GetVideoState;
using AlphaZero.Modules.VideoUploading.Application.Commands.Delete;
using AlphaZero.API.Shared;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AlphaZero.Modules.VideoUploading.Presentation.Features;

public static class Debug
{
    public class GetVideosEndpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("api/video-uploading/debug/videos", Handler)
               .WithTags("Video Uploading Debug");
        }

        private async Task<IResult> Handler(int? page, int? perPage, VideoUploadingModule module)
        {
            var query = new ListVideosQuery(page ?? 1, perPage ?? 10);
            var response = await module.Send<ListVideosQuery, ErrorOr<AlphaZero.Shared.Queries.PagedResult<AlphaZero.Modules.VideoUploading.Domain.Models.Video>>>(query);
            return response.Match(
                res => Results.Ok(res),
                errors => errors.ToMinimalResult());
        }
    }

    public class GetVideoEndpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("api/video-uploading/debug/videos/{id:guid}", Handler)
               .WithTags("Video Uploading Debug");
        }

        private async Task<IResult> Handler(Guid id, VideoUploadingModule module)
        {
            var query = new GetVideoQuery(id);
            var response = await module.Send<GetVideoQuery, ErrorOr<AlphaZero.Modules.VideoUploading.Domain.Models.Video>>(query);
            return response.Match(
                res => Results.Ok(res),
                errors => errors.ToMinimalResult());
        }
    }

    public class GetVideoStateEndpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("api/video-uploading/debug/videos/{id:guid}/state", Handler)
               .WithTags("Video Uploading Debug");
        }

        private async Task<IResult> Handler(Guid id, VideoUploadingModule module)
        {
            var query = new GetVideoStateQuery(id);
            var response = await module.Send<GetVideoStateQuery, ErrorOr<AlphaZero.Modules.VideoUploading.Application.Repositories.VideoStateDto>>(query);
            return response.Match(
                res => Results.Ok(res),
                errors => errors.ToMinimalResult());
        }
    }/*

    public class GetStreamingInfoEndpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("api/video-uploading/debug/videos/{id:guid}/streaming", Handler)
               .WithTags("Video Uploading Debug");
        }

        private async Task<IResult> Handler(Guid id, VideoUploadingModule module)
        {
            var query = new AlphaZero.Modules.VideoUploading.Application.Queries.GetStreamingInfo.GetStreamingInfoQuery(id);
            var response = await module.Send<AlphaZero.Modules.VideoUploading.Application.Queries.GetStreamingInfo.GetStreamingInfoQuery, ErrorOr<AlphaZero.Modules.VideoUploading.Application.Queries.GetStreamingInfo.StreamingInfo>>(query);
            return response.Match(
                res => Results.Ok(res),
                errors => errors.ToMinimalResult());
        }
    }*/

    public class DeleteVideoEndpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapDelete("api/video-uploading/debug/videos/{id:guid}", Handler)
               .WithTags("Video Uploading Debug");
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
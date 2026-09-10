using AlphaZero.API.Shared;
using AlphaZero.Modules.Documents.Application.Commands.UploadDocument;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AlphaZero.Modules.Documents.Presentation.Endpoints;

public static class UploadDocument
{
    public record Request(
        string Title,
        string? Description,
        string FileName,
        string ContentType,
        long FileSizeBytes);

    public record Response(
        Guid DocumentId,
        string Arn,
        string UploadPresignedUrl,
        string S3Key);

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("api/documents/upload", Handler)
                .WithTags("Documents")
                .AccessControl("documents:Upload", (req, tenantId) => ResourceArn.ForTenant(tenantId))
                .WithSummary("Requests pre-signed URL for document upload")
                .WithDescription("Initializes a document upload session and returns S3 pre-signed upload URL.")
                .Produces<Response>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status400BadRequest)
                .ProducesProblem(StatusCodes.Status401Unauthorized)
                .ProducesProblem(StatusCodes.Status403Forbidden);
        }

        private async Task<IResult> Handler(Request request, DocumentsModule module, HttpContext context)
        {
            var command = new UploadDocumentCommand(
                request.Title,
                request.Description,
                request.FileName,
                request.ContentType,
                request.FileSizeBytes);

            var response = await module.Send<UploadDocumentCommand, ErrorOr<UploadDocumentResponse>>(command);

            return response.Match(
                res => Results.Ok(new Response(
                    res.DocumentId,
                    res.Arn,
                    res.UploadPresignedUrl,
                    res.S3Key)),
                errors => errors.ToMinimalResult());
        }
    }
}

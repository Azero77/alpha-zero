using AlphaZero.API.Shared;
using AlphaZero.Modules.Documents.Application.Commands.DeleteDocument;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AlphaZero.Modules.Documents.Presentation.Endpoints;

public static class DeleteDocument
{
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapDelete("api/documents/{id:guid}", Handler)
                .WithTags("Documents")
                .AccessControl("documents:Delete", (ctx, tenantId) =>
                {
                    var idStr = ctx.Request.RouteValues["id"]?.ToString();
                    var docId = Guid.TryParse(idStr, out var id) ? id : Guid.Empty;
                    return ResourceArn.ForDocument(tenantId, docId);
                })
                .WithSummary("Soft-deletes a document")
                .Produces(StatusCodes.Status204NoContent)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status401Unauthorized)
                .ProducesProblem(StatusCodes.Status403Forbidden);
        }

        private async Task<IResult> Handler(Guid id, DocumentsModule module, HttpContext context)
        {
            var command = new DeleteDocumentCommand(id);
            var response = await module.Send<DeleteDocumentCommand, ErrorOr<Success>>(command);

            return response.Match(
                _ => Results.NoContent(),
                errors => errors.ToMinimalResult());
        }
    }
}

using AlphaZero.API.Shared;
using AlphaZero.Modules.Documents.Application.Queries.GetDocument;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AlphaZero.Modules.Documents.Presentation.Endpoints;

public static class GetDocument
{
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("api/documents/{id:guid}", Handler)
                .WithTags("Documents")
                .AccessControl("documents:View", (ctx, tenantId) =>
                {
                    var idStr = ctx.Request.RouteValues["id"]?.ToString();
                    var docId = Guid.TryParse(idStr, out var id) ? id : Guid.Empty;
                    return ResourceArn.ForDocument(tenantId, docId);
                })
                .WithSummary("Gets document details and presigned download URL")
                .Produces<DocumentDetailsResponse>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status401Unauthorized)
                .ProducesProblem(StatusCodes.Status403Forbidden);
        }

        private async Task<IResult> Handler(Guid id, DocumentsModule module, HttpContext context)
        {
            var query = new GetDocumentQuery(id);
            var response = await module.Send<GetDocumentQuery, ErrorOr<DocumentDetailsResponse>>(query);

            return response.Match(
                res => Results.Ok(res),
                errors => errors.ToMinimalResult());
        }
    }
}

using AlphaZero.API.Shared;
using AlphaZero.Modules.Documents.Application.Queries.ListDocuments;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AlphaZero.Modules.Documents.Presentation.Endpoints;

public static class ListDocuments
{
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("api/documents", Handler)
                .WithTags("Documents")
                .AccessControl("documents:View", (req, tenantId) => ResourceArn.ForTenant(tenantId))
                .WithSummary("Lists documents for the tenant")
                .WithDescription("Returns a list of documents in the tenant scope for browsing in the Asset Library.")
                .Produces<List<DocumentSummaryResponse>>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status401Unauthorized)
                .ProducesProblem(StatusCodes.Status403Forbidden);
        }

        private async Task<IResult> Handler(string? type, DocumentsModule module, HttpContext context)
        {
            var query = new ListDocumentsQuery(type);
            var response = await module.Send<ListDocumentsQuery, ErrorOr<List<DocumentSummaryResponse>>>(query);

            return response.Match(
                res => Results.Ok(res),
                errors => errors.ToMinimalResult());
        }
    }
}

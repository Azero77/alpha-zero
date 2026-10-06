using AlphaZero.Modules.Documents.Application.Queries.GetDocumentDownloadUrl;
using AlphaZero.API.Shared;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AlphaZero.Modules.Documents.Presentation.Endpoints;

public class GetDocumentDownloadUrl : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("documents/{documentId:guid}/download-url", async (Guid documentId, ISender sender) =>
        {
            var query = new GetDocumentDownloadUrlQuery(documentId);
            var result = await sender.Send(query);

            return result.Match(
                response => Results.Ok(response),
                CustomResults.Problem
            );
        })
        .WithTags("Documents")
        .RequireAuthorization();
    }
}

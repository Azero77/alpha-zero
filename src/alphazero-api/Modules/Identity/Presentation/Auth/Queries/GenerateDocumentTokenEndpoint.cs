using AlphaZero.API.Shared;
using AlphaZero.Modules.Identity.Application.Auth.Queries.GenerateDocumentToken;
using AlphaZero.Shared.Presentation.Extensions;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AlphaZero.Modules.Identity.Presentation.Auth.Queries;

public class GenerateDocumentTokenEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/identity/tokens/document", Handler)
            .WithTags("Identity Auth")
            .WithSummary("Generates a CDN scope token for documents")
            .WithDescription("Requires tenant authorization.")
            .Produces<GenerateDocumentTokenResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    private async Task<IResult> Handler([Microsoft.AspNetCore.Mvc.FromQuery] string scope, IdentityModule module, HttpContext context)
    {
        var query = new GenerateDocumentTokenQuery(scope);
        var response = await module.Send<GenerateDocumentTokenQuery, ErrorOr<GenerateDocumentTokenResponse>>(query);

        return response.Match(
            res => Results.Ok(res),
            errors => errors.ToMinimalResult());
    }
}

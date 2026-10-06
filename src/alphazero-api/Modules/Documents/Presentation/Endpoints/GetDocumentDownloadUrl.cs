using AlphaZero.Modules.Documents.Application.Queries.GetDocumentDownloadUrl;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;

namespace AlphaZero.Modules.Documents.Presentation.Endpoints;

public record GetDocumentDownloadUrlRequest { public Guid DocumentId { get; init; } }

public class GetDocumentDownloadUrlSummary : Summary<GetDocumentDownloadUrlEndpoint>
{
    public GetDocumentDownloadUrlSummary()
    {
        Summary = "Generates a download URL for a document";
        Description = "Returns a pre-signed S3 URL for secure downloading of the document.";
        Response<GetDocumentDownloadUrlResponse>(200, "URL generated successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Document not found");
    }
}

public class GetDocumentDownloadUrlEndpoint : Endpoint<GetDocumentDownloadUrlRequest, GetDocumentDownloadUrlResponse>
{
    private readonly DocumentsModule _module;

    public GetDocumentDownloadUrlEndpoint(DocumentsModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Get("/documents/{DocumentId}/download-url");
        this.AccessControl("content:read", (req, tenantId) => ResourceArn.ForDocument(tenantId, req.DocumentId));
        Description(d => d.WithTags("Documents"));
        Summary(new GetDocumentDownloadUrlSummary());
    }

    public override async Task HandleAsync(GetDocumentDownloadUrlRequest req, CancellationToken ct)
    {
        var query = new GetDocumentDownloadUrlQuery(req.DocumentId);
        var result = await _module.Send(query, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}

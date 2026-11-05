using AlphaZero.Modules.Library.Application.AccessCodes.SellCode;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Library.Presentation.Endpoints.AccessCodes.SellCode;

public record SellCodeRequest
{
    public string RawCode { get; init; } = default!;
    public Guid? LibraryId { get; init; }
}

public record SellCodeResponse
{
    public Guid Id { get; init; }
    public Guid AccessCodeId { get; init; }
    public Guid LibraryId { get; init; }
    public decimal Price { get; init; }
    public string Currency { get; init; } = default!;
    public DateTime SoldAt { get; init; }
}

public class SellCodeSummary : Summary<SellCodeEndpoint>
{
    public SellCodeSummary()
    {
        Summary = "Records the point-of-sale sale of an access code";
        Description = "Moves a distributed access code to Sold status and creates an immutable SaleRecord.";
        Response<SellCodeResponse>(201, "Sale recorded successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (RawCode empty or missing library)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized (User unauthenticated)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing library:SellCodes permission or tenant mismatch)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Access code not found (AccessCode.NotFound)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(409, "Conflict: code not distributed or already sold");
    }
}

public class SellCodeEndpoint(LibraryModule module) : Endpoint<SellCodeRequest, SellCodeResponse>
{
    public override void Configure()
    {
        Post("/library/access-codes/sell");
        this.AccessControl("library:SellCodes", (req, tenantId) => req.LibraryId.HasValue ? ResourceArn.ForLibrary(tenantId, req.LibraryId.Value) : ResourceArn.ForTenant(tenantId));
        Description(d => d.WithTags("Library"));
        Summary(new SellCodeSummary());
    }

    public override async Task HandleAsync(SellCodeRequest req, CancellationToken ct)
    {
        var command = new SellCodeCommand(req.RawCode, req.LibraryId);
        var result = await module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        var response = new SellCodeResponse
        {
            Id = result.Value.SaleId,
            AccessCodeId = result.Value.AccessCodeId,
            LibraryId = result.Value.LibraryId,
            Price = result.Value.Price,
            Currency = result.Value.Currency,
            SoldAt = result.Value.SoldAt
        };

        await Send.CreatedAtAsync($"/library/access-codes/sales/{response.Id}", responseBody: response, cancellation: ct);
    }
}

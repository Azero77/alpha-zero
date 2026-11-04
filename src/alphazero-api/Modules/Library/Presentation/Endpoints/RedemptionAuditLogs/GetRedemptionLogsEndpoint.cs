using AlphaZero.Modules.Library.Application.RedemptionAuditLogs.GetRedemptionLogs;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Presentation.Extensions;
using AlphaZero.Shared.Queries;
using FastEndpoints;
using FluentValidation;

namespace AlphaZero.Modules.Library.Presentation.Endpoints.RedemptionAuditLogs;

public class GetRedemptionLogsSummary : Summary<GetRedemptionLogsEndpoint>
{
    public GetRedemptionLogsSummary()
    {
        Summary = "Gets redemption audit logs";
        Description = "Retrieves paginated redemption audit logs for a library.";
        Response<PagedResult<RedemptionAuditLogDto>>(200, "Redemption audit logs retrieved successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (LibraryId empty or invalid)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing library:Audit permission)");
    }
}

public class GetRedemptionLogsRequestValidator : Validator<GetRedemptionLogsRequest>
{
    public GetRedemptionLogsRequestValidator()
    {
        RuleFor(x => x.LibraryId)
            .NotEmpty()
            .WithMessage("LibraryId is required.");
    }
}

public class GetRedemptionLogsEndpoint : Endpoint<GetRedemptionLogsRequest, PagedResult<RedemptionAuditLogDto>>
{
    private readonly LibraryModule _module;

    public GetRedemptionLogsEndpoint(LibraryModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Get("/library/libraries/{LibraryId}/audit-logs", "/library/audit-logs");
        this.AccessControl("library:Audit", (req, tenantId) => ResourceArn.ForLibrary(tenantId, req.LibraryId.GetValueOrDefault()));
        Summary(new GetRedemptionLogsSummary());
    }

    public override async Task HandleAsync(GetRedemptionLogsRequest req, CancellationToken ct)
    {
        var query = new GetRedemptionLogsQuery(
            req.LibraryId!.Value,
            req.From,
            req.To,
            req.Page,
            req.PageSize);

        var result = await _module.Send(query, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}

public record GetRedemptionLogsRequest(
    Guid? LibraryId,
    DateOnly? From,
    DateOnly? To,
    int Page = 1,
    int PageSize = 50);

using AlphaZero.Modules.Library.Application.RedemptionAuditLogs.GetRedemptionLogs;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Presentation.Extensions;
using AlphaZero.Shared.Queries;
using FastEndpoints;
using MediatR;

namespace AlphaZero.Modules.Library.Presentation.Endpoints.RedemptionAuditLogs;

public class GetRedemptionLogsSummary : Summary<GetRedemptionLogsEndpoint>
{
    public GetRedemptionLogsSummary()
    {
        Summary = "Gets redemption audit logs";
        Description = "Retrieves paginated redemption audit logs for a library or tenant.";
        Response<PagedResult<RedemptionAuditLogDto>>(200, "Redemption audit logs retrieved successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing library:Audit permission)");
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
        Get("/library/libraries/{LibraryId}/audit-logs");
        AllowAnonymous(); // Auth handled by AccessControl middleware

        this.AccessControl("library:Audit", (req, tenantId) =>
        {
            if (req.LibraryId.HasValue)
            {
                return ResourceArn.ForLibrary(tenantId, req.LibraryId.Value);
            }
            return ResourceArn.ForTenant(tenantId);
        });
        Summary(new GetRedemptionLogsSummary());
    }

    public override async Task HandleAsync(GetRedemptionLogsRequest req, CancellationToken ct)
    {
        var query = new GetRedemptionLogsQuery(
            req.LibraryId,
            req.From,
            req.To,
            req.Page,
            req.PageSize);

        var result = await _module.Send(query, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors);
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

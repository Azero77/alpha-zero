using AlphaZero.Modules.Library.Application.Queries;
using AlphaZero.Shared.Queries;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Library.Application.RedemptionAuditLogs.GetRedemptionLogs;

public sealed class GetRedemptionLogsQueryHandler(ILibraryQueryService libraryQueryService) : IRequestHandler<GetRedemptionLogsQuery, ErrorOr<PagedResult<RedemptionAuditLogDto>>>
{
    public async Task<ErrorOr<PagedResult<RedemptionAuditLogDto>>> Handle(GetRedemptionLogsQuery request, CancellationToken cancellationToken)
    {
        var result = await libraryQueryService.GetRedemptionLogsAsync(
            request.LibraryId,
            request.From,
            request.To,
            request.Page,
            request.PageSize,
            cancellationToken);

        return result;
    }
}

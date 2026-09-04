using AlphaZero.Shared.Queries;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Library.Application.RedemptionAuditLogs.GetRedemptionLogs;

public record GetRedemptionLogsQuery(
    Guid? LibraryId,
    DateOnly? From,
    DateOnly? To,
    int Page = 1,
    int PageSize = 50
) : IRequest<ErrorOr<PagedResult<RedemptionAuditLogDto>>>;

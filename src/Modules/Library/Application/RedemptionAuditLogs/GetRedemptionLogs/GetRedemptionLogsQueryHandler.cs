using AlphaZero.Modules.Library.Domain;
using AlphaZero.Shared.Queries;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Library.Application.RedemptionAuditLogs.GetRedemptionLogs;

public class GetRedemptionLogsQueryHandler : IRequestHandler<GetRedemptionLogsQuery, ErrorOr<PagedResult<RedemptionAuditLogDto>>>
{
    private readonly IRedemptionAuditLogRepository _repository;

    public GetRedemptionLogsQueryHandler(IRedemptionAuditLogRepository repository)
    {
        _repository = repository;
    }

    public async Task<ErrorOr<PagedResult<RedemptionAuditLogDto>>> Handle(GetRedemptionLogsQuery request, CancellationToken cancellationToken)
    {
        var result = await _repository.GetPagedAsync(
            request.LibraryId,
            request.From,
            request.To,
            request.Page,
            request.PageSize,
            cancellationToken);

        var dtos = result.Items.Select(x => new RedemptionAuditLogDto(
            x.Id,
            x.AccessCodeId,
            x.LibraryId,
            x.RedeemedByUserId,
            x.StrategyId,
            x.TargetResourceArn.Value,
            x.RedeemedAt,
            x.IpAddress,
            x.DeviceFingerprint
        )).ToList();

        return new PagedResult<RedemptionAuditLogDto>(dtos, result.TotalCount, result.CurrentPage, result.PageSize);
    }
}

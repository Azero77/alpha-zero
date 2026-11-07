using MediatR;
using ErrorOr;
using AlphaZero.Modules.Library.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Authorization;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Library.Application.RedeemCode;

public class RedeemCodeCommandHandler : IRequestHandler<RedeemCodeCommand, ErrorOr<Success>>
{
    private readonly IAccessCodeRepository _repository;
    private readonly IRedemptionAuditLogRepository _auditLogRepository;
    private readonly ISaleRecordRepository _saleRecordRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentTenantUserRepository _currentUserRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuthorizationContextProvider _authorizationContextProvider;
    private readonly ILogger<RedeemCodeCommandHandler> _logger;

    public RedeemCodeCommandHandler(
        IAccessCodeRepository repository,
        IRedemptionAuditLogRepository auditLogRepository,
        ISaleRecordRepository saleRecordRepository,
        ITenantProvider tenantProvider,
        ICurrentTenantUserRepository currentUserRepository,
        IPasswordHasher passwordHasher,
        IAuthorizationContextProvider authorizationContextProvider,
        ILogger<RedeemCodeCommandHandler> logger)
    {
        _repository = repository;
        _auditLogRepository = auditLogRepository;
        _saleRecordRepository = saleRecordRepository;
        _tenantProvider = tenantProvider;
        _currentUserRepository = currentUserRepository;
        _passwordHasher = passwordHasher;
        _authorizationContextProvider = authorizationContextProvider;
        _logger = logger;
    }

    public async Task<ErrorOr<Success>> Handle(RedeemCodeCommand request, CancellationToken cancellationToken)
    {
        // Lock ordering discipline (prevents deadlocks between concurrent redeem/void flows):
        // 1. AccessCode row (via SELECT ... FOR UPDATE)
        // 2. RedemptionAuditLog row (INSERT)
        // 3. Audit stream projection (P3b)

        // 1. Authenticate first (401 per spec)
        var currentUser = await _currentUserRepository.GetCurrentUser();
        if (currentUser == null)
        {
            return Error.Unauthorized("User.Unauthenticated", "User must be logged in to redeem codes.");
        }

        // 2. Hash the code and verify existence with pessimistic row-level lock (404 per spec)
        var hash = _passwordHasher.HashPassword(request.RawCode);
        var accessCode = await _repository.GetByHashForUpdateAsync(hash, cancellationToken);
        if (accessCode == null)
        {
            return Error.NotFound("AccessCode.NotFound", "The provided code is invalid.");
        }

        // 3. Validate Tenant match (403 per spec)
        var currentTenantId = _tenantProvider.GetTenant();
        if (accessCode.TenantId != currentTenantId)
        {
             return Error.Forbidden("AccessCode.TenantMismatch", "This code belongs to another academy.");
        }

        // Natural Idempotency: If this exact student already redeemed this code, safely return Success
        if (accessCode.Status == AccessCodeStatus.Redeemed && accessCode.RedeemedByUserId == currentUser.UserId)
        {
            return Result.Success;
        }

        // Fraud check (P3a, issue #59 queue source): Lax gate — absence of sale does not block
        // redemption, but raises the UnsoldRedemption fraud flag into the audit stream payload.
        var hasSaleRecord = await _saleRecordRepository.ExistsForCodeAsync(accessCode.Id, cancellationToken);
        var isUnsoldRedemption = !hasSaleRecord;
        if (isUnsoldRedemption)
        {
            _logger.LogWarning("Fraud flag: UnsoldRedemption for Access Code {CodeId}. Redeemed without prior sale.", accessCode.Id);
        }

        var deviceFingerprint = request.DeviceFingerprint ?? _authorizationContextProvider?.CurrentAuthorizationContext?.DeviceId;
        var ipAddress = request.IpAddress ?? _authorizationContextProvider?.CurrentAuthorizationContext?.IpAddress;
        var now = DateTime.UtcNow;

        // Record Forensic Audit Log
        var redemptionContext = new RedemptionContext(
            accessCode.TenantId,
            accessCode.LibraryId,
            currentUser.UserId,
            accessCode.StrategyId,
            accessCode.TargetResourceArn,
            now,
            ipAddress,
            deviceFingerprint);

        var auditLog = RedemptionAuditLog.Record(redemptionContext, accessCode.Id);

        // 5. Redeem in Domain (passes forensicLogId and unsoldRedemption flag to domain event / audit stream).
        // Provisioning is producer-only (#58): AccessCodeActivatedDomainEventHandler publishes
        // AccessCodeActivatedIntegrationEvent from the redeemed domain event — no inline strategy execution.
        var redeemResult = accessCode.Redeem(currentUser.UserId, now, forensicLogId: auditLog.Id, isUnsoldRedemption: isUnsoldRedemption);
        if (redeemResult.IsError)
        {
            return redeemResult.Errors;
        }

        await _auditLogRepository.AddAsync(auditLog, cancellationToken);

        var updateResult = await _repository.UpdateAsync(accessCode, cancellationToken);
        if (updateResult.IsError)
        {
            return updateResult.Errors;
        }

        return Result.Success;
    }
}

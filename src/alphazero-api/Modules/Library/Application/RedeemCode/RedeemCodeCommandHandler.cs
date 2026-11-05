using MediatR;
using ErrorOr;
using AlphaZero.Modules.Library.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Authorization;

namespace AlphaZero.Modules.Library.Application.RedeemCode;

public class RedeemCodeCommandHandler : IRequestHandler<RedeemCodeCommand, ErrorOr<Success>>
{
    private readonly IAccessCodeRepository _repository;
    private readonly IRedemptionAuditLogRepository _auditLogRepository;
    private readonly IRedemptionStrategyFactory _strategyFactory;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentTenantUserRepository _currentUserRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuthorizationContextProvider _authorizationContextProvider;

    public RedeemCodeCommandHandler(
        IAccessCodeRepository repository,
        IRedemptionAuditLogRepository auditLogRepository,
        IRedemptionStrategyFactory strategyFactory,
        ITenantProvider tenantProvider,
        ICurrentTenantUserRepository currentUserRepository,
        IPasswordHasher passwordHasher,
        IAuthorizationContextProvider authorizationContextProvider)
    {
        _repository = repository;
        _auditLogRepository = auditLogRepository;
        _strategyFactory = strategyFactory;
        _tenantProvider = tenantProvider;
        _currentUserRepository = currentUserRepository;
        _passwordHasher = passwordHasher;
        _authorizationContextProvider = authorizationContextProvider;
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

        // 🛡️ Natural Idempotency: If this exact student already redeemed this code, safely return Success
        if (accessCode.Status == AccessCodeStatus.Redeemed && accessCode.RedeemedByUserId == currentUser.UserId)
        {
            return Result.Success;
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

        // 5. Redeem in Domain (passes auditLog.Id to event and audit stream)
        var redeemResult = accessCode.Redeem(currentUser.UserId, now, auditLog.Id);
        if (redeemResult.IsError)
        {
            return redeemResult.Errors;
        }
            
        // 6. Execute Strategy
        var strategy = _strategyFactory.GetStrategy(accessCode.StrategyId);
        await strategy.ExecuteAsync(currentUser.UserId, accessCode.Id, accessCode.TargetResourceArn, accessCode.Metadata.RootElement);

        await _auditLogRepository.AddAsync(auditLog, cancellationToken);

        var updateResult = await _repository.UpdateAsync(accessCode, cancellationToken);
        if (updateResult.IsError)
        {
            return updateResult.Errors;
        }

        return Result.Success;
    }
}

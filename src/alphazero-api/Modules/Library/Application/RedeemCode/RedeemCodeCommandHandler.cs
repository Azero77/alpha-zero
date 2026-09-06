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
    private readonly IAuthorizationContextFactory authorizationContextFactory;

    public RedeemCodeCommandHandler(
        IAccessCodeRepository repository,
        IRedemptionAuditLogRepository auditLogRepository,
        IRedemptionStrategyFactory strategyFactory,
        ITenantProvider tenantProvider,
        ICurrentTenantUserRepository currentUserRepository,
        IPasswordHasher passwordHasher,
        IAuthorizationContextFactory authorizationContextFactory)
    {
        _repository = repository;
        _auditLogRepository = auditLogRepository;
        _strategyFactory = strategyFactory;
        _tenantProvider = tenantProvider;
        _currentUserRepository = currentUserRepository;
        _passwordHasher = passwordHasher;
        this.authorizationContextFactory = authorizationContextFactory;
    }

    public async Task<ErrorOr<Success>> Handle(RedeemCodeCommand request, CancellationToken cancellationToken)
    {
        // 1. Hash the code
        var hash = _passwordHasher.HashPassword(request.RawCode);
        
        // 2. Find the code
        var accessCode = await _repository.GetByHashAsync(hash, cancellationToken);
        if (accessCode == null)
        {
            return Error.NotFound("AccessCode.NotFound", "The provided code is invalid.");
        }

        // 3. Validate Tenant
        var currentTenantId = _tenantProvider.GetTenant();
        if (accessCode.TenantId != currentTenantId)
        {
             return Error.Forbidden("AccessCode.TenantMismatch", "This code belongs to another academy.");
        }

        // 4. Get Current User
        var currentUser = await _currentUserRepository.GetCurrentUser();
        if (currentUser == null)
        {
            return Error.Unauthorized("User.Unauthenticated", "User must be logged in to redeem codes.");
        }

        // 5. Redeem in Domain
        var redeemResult = accessCode.Redeem(currentUser.UserId);
        if (redeemResult.IsError)
        {
            return redeemResult.Errors;
        }
            
        // 6. Execute Strategy
        var strategy = _strategyFactory.GetStrategy(accessCode.StrategyId);
        await strategy.ExecuteAsync(currentUser.UserId, accessCode.Id,accessCode.TargetResourceArn, accessCode.Metadata.RootElement);

        var deviceFingerprint = request.DeviceFingerprint ??  authorizationContextFactory?.CurrentAuthorizationContext?.DeviceId;
        var ipAddress = request.IpAddress ??  authorizationContextFactory?.CurrentAuthorizationContext?.IpAddress;

        // 7. Record Audit Log
        var auditLog = RedemptionAuditLog.Record(
            accessCode.TenantId,
            accessCode.LibraryId,
            accessCode.Id,
            currentUser.UserId,
            accessCode.StrategyId,
            accessCode.TargetResourceArn,
            accessCode.RedeemedAt!.Value,
            ipAddress,
            deviceFingerprint);

        await _auditLogRepository.AddAsync(auditLog, cancellationToken);

        _repository.Update(accessCode);
        return Result.Success;
    }
}

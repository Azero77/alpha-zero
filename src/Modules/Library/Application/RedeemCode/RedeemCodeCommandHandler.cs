using MediatR;
using ErrorOr;
using AlphaZero.Modules.Library.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Application;

namespace AlphaZero.Modules.Library.Application.RedeemCode;

public class RedeemCodeCommandHandler : IRequestHandler<RedeemCodeCommand, ErrorOr<Success>>
{
    private readonly IAccessCodeRepository _repository;
    private readonly IRedemptionStrategyFactory _strategyFactory;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentTenantUserRepository _currentUserRepository;
    private readonly IPasswordHasher _passwordHasher;

    public RedeemCodeCommandHandler(
        IAccessCodeRepository repository,
        IRedemptionStrategyFactory strategyFactory,
        ITenantProvider tenantProvider,
        ICurrentTenantUserRepository currentUserRepository,
        IPasswordHasher passwordHasher
        )
    {
        _repository = repository;
        _strategyFactory = strategyFactory;
        _tenantProvider = tenantProvider;
        _currentUserRepository = currentUserRepository;
        _passwordHasher = passwordHasher;
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

        _repository.Update(accessCode);
        return Result.Success;
    }
}

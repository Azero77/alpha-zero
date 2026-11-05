using System.Text.Json;
using AlphaZero.Modules.Library.Domain;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Library.Application.AccessCodes.SellCode;

public record SellCodeResult(
    Guid SaleId,
    Guid AccessCodeId,
    Guid LibraryId,
    decimal Price,
    string Currency,
    DateTime SoldAt);

public record SellCodeCommand(string RawCode, Guid? LibraryId = null) : ICommand<SellCodeResult>, ITransactionalCommand;

public class SellCodeCommandValidator : AbstractValidator<SellCodeCommand>
{
    public SellCodeCommandValidator()
    {
        RuleFor(x => x.RawCode).NotEmpty();
    }
}

public sealed class SellCodeCommandHandler : IRequestHandler<SellCodeCommand, ErrorOr<SellCodeResult>>
{
    private readonly IAccessCodeRepository _accessCodeRepository;
    private readonly ISaleRecordRepository _saleRecordRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentTenantUserRepository _currentUserRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPolicyEvaluatorService? _policyEvaluatorService;
    private readonly IAuthorizationContextProvider? _authorizationContextProvider;
    private readonly ILogger<SellCodeCommandHandler> _logger;

    public SellCodeCommandHandler(
        IAccessCodeRepository accessCodeRepository,
        ISaleRecordRepository saleRecordRepository,
        ITenantProvider tenantProvider,
        ICurrentTenantUserRepository currentUserRepository,
        IPasswordHasher passwordHasher,
        ILogger<SellCodeCommandHandler> logger,
        IPolicyEvaluatorService? policyEvaluatorService = null,
        IAuthorizationContextProvider? authorizationContextProvider = null)
    {
        _accessCodeRepository = accessCodeRepository;
        _saleRecordRepository = saleRecordRepository;
        _tenantProvider = tenantProvider;
        _currentUserRepository = currentUserRepository;
        _passwordHasher = passwordHasher;
        _policyEvaluatorService = policyEvaluatorService;
        _authorizationContextProvider = authorizationContextProvider;
        _logger = logger;
    }

    public async Task<ErrorOr<SellCodeResult>> Handle(SellCodeCommand request, CancellationToken cancellationToken)
    {
        // 1. Authenticate selling actor
        var currentUser = await _currentUserRepository.GetCurrentUser();
        if (currentUser == null)
        {
            return Error.Unauthorized("User.Unauthenticated", "User must be logged in to record sales.");
        }

        // 2. Hash raw code and load access code with pessimistic lock
        var hash = _passwordHasher.HashPassword(request.RawCode);
        var accessCode = await _accessCodeRepository.GetByHashForUpdateAsync(hash, cancellationToken);
        if (accessCode == null)
        {
            return Error.NotFound("AccessCode.NotFound", "The provided code is invalid.");
        }

        // 3. Verify Tenant match
        var currentTenantId = _tenantProvider.GetTenant();
        if (accessCode.TenantId != currentTenantId)
        {
            return Error.Forbidden("AccessCode.TenantMismatch", "This code belongs to another academy.");
        }

        // 4. Resolve library id.
        // NOTE (auth fix): when the code is UNASSIGNED (LibraryId null — an admin-controlled
        // code), request.LibraryId is NOT the code's owner; it is merely where the seller
        // registers the sale. Authorizing the sale against the caller-supplied library ARN would
        // let a library-scoped caller sell another's unassigned code into their own library.
        // Such sales therefore require a TENANT-level SellCodes grant; assigned codes keep the
        // library-level grant on the code's OWNER library.
        var libraryId = accessCode.LibraryId ?? request.LibraryId;
        if (!libraryId.HasValue)
        {
            return Error.Validation("AccessCode.MissingLibrary", "A library must be associated with the access code to record a sale.");
        }

        // 5. Evaluate permission on the code's owning scope
        if (_policyEvaluatorService != null)
        {
            var baseContext = _authorizationContextProvider?.CurrentAuthorizationContext ?? new AuthorizationContext
            {
                AuthenticationMethod = "TenantUser"
            };
            var resourceArn = accessCode.LibraryId.HasValue
                ? ResourceArn.ForLibrary(accessCode.TenantId, accessCode.LibraryId.Value)
                : ResourceArn.ForTenant(accessCode.TenantId);
            var authContext = baseContext.ForResource("library:SellCodes", resourceArn);

            var authResult = await _policyEvaluatorService.Authorize(authContext);
            if (authResult.IsError)
            {
                return authResult.Errors;
            }
        }

        // 6. Check existing sale record (unique active sale)
        var alreadyHasSale = await _saleRecordRepository.ExistsForCodeAsync(accessCode.Id, cancellationToken);
        if (alreadyHasSale || accessCode.Status == AccessCodeStatus.Sold)
        {
            return Error.Conflict("AccessCode.AlreadySold", "Code has already been sold.");
        }

        // 7. Snapshot batch face value (price) and currency from code metadata
        decimal price = 0m;
        if (accessCode.Metadata != null && accessCode.Metadata.RootElement.TryGetProperty("FaceValue", out var fvProp))
        {
            if (fvProp.ValueKind == JsonValueKind.Number && fvProp.TryGetDecimal(out var decVal))
            {
                price = decVal;
            }
        }

        string currency = "USD";
        if (accessCode.Metadata != null && accessCode.Metadata.RootElement.TryGetProperty("Currency", out var curProp))
        {
            var curStr = curProp.GetString();
            if (!string.IsNullOrWhiteSpace(curStr))
            {
                currency = curStr.ToUpperInvariant();
            }
        }

        // 8. Execute state transition: Distributed -> Sold
        var sellResult = accessCode.Sell(libraryId.Value, currentUser.UserId, price, currency);
        if (sellResult.IsError)
        {
            return sellResult.Errors;
        }

        // 9. Create and persist SaleRecord (immutable snapshot)
        var saleRecord = SaleRecord.Create(
            accessCode.TenantId,
            libraryId.Value,
            accessCode.Id,
            currentUser.UserId,
            price,
            currency);

        await _saleRecordRepository.AddAsync(saleRecord, cancellationToken);

        // 10. Persist AccessCode update with tripwire
        var updateResult = await _accessCodeRepository.UpdateAsync(accessCode, cancellationToken);
        if (updateResult.IsError)
        {
            return updateResult.Errors;
        }

        _logger.LogInformation("Recorded sale {SaleId} for AccessCode {CodeId} at Library {LibraryId} by User {UserId}. Price: {Price} {Currency}",
            saleRecord.Id, accessCode.Id, libraryId.Value, currentUser.UserId, price, currency);

        return new SellCodeResult(
            saleRecord.Id,
            saleRecord.AccessCodeId,
            saleRecord.LibraryId,
            saleRecord.Price,
            saleRecord.Currency,
            saleRecord.SoldAt);
    }
}

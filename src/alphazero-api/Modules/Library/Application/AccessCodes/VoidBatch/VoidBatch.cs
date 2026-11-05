using AlphaZero.Modules.Library.Domain;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Library.Application.AccessCodes.VoidBatch;

public record VoidBatchResult(
    Guid BatchId,
    int TotalCodes,
    int VoidedCount,
    int SkippedCount,
    List<Guid> VoidedCodeIds);

public record VoidBatchCommand(Guid BatchId, string Reason) : ICommand<VoidBatchResult>, ITransactionalCommand;

public class VoidBatchCommandValidator : AbstractValidator<VoidBatchCommand>
{
    public VoidBatchCommandValidator()
    {
        RuleFor(x => x.BatchId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(512);
    }
}

public sealed class VoidBatchCommandHandler : IRequestHandler<VoidBatchCommand, ErrorOr<VoidBatchResult>>
{
    public const int MaxRecallBatchSize = 500;

    private readonly IAccessCodeRepository _repository;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<VoidBatchCommandHandler> _logger;
    private readonly ICurrentTenantUserRepository? _currentUserRepository;
    private readonly IPolicyEvaluatorService? _policyEvaluatorService;
    private readonly IAuthorizationContextProvider? _authorizationContextProvider;

    public VoidBatchCommandHandler(
        IAccessCodeRepository repository,
        ITenantProvider tenantProvider,
        ILogger<VoidBatchCommandHandler> logger,
        ICurrentTenantUserRepository? currentUserRepository = null,
        IPolicyEvaluatorService? policyEvaluatorService = null,
        IAuthorizationContextProvider? authorizationContextProvider = null)
    {
        _repository = repository;
        _tenantProvider = tenantProvider;
        _logger = logger;
        _currentUserRepository = currentUserRepository;
        _policyEvaluatorService = policyEvaluatorService;
        _authorizationContextProvider = authorizationContextProvider;
    }

    public async Task<ErrorOr<VoidBatchResult>> Handle(VoidBatchCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenant();
        if (tenantId is null)
        {
            return Error.Unauthorized("Tenant.NotFound", "Tenant not found.");
        }

        // 1. Validate batch existence and size cap BEFORE acquiring row-level locks
        var totalCount = await _repository.CountBatchAsync(request.BatchId, cancellationToken);
        if (totalCount == 0)
        {
            var batchExists = await _repository.BatchExistsAsync(request.BatchId, cancellationToken);
            if (!batchExists)
            {
                return Error.NotFound("Batch.NotFound", $"Access code batch '{request.BatchId}' does not exist.");
            }
        }

        if (totalCount > MaxRecallBatchSize)
        {
            return Error.Validation(
                "Batch.OverCap",
                $"Batch contains {totalCount} codes, which exceeds the maximum allowed recall cap of {MaxRecallBatchSize} codes. Zero codes were modified.");
        }

        // 2. Lock ordering discipline: Acquire row locks in deterministic Order By Id FOR UPDATE.
        // The tenant predicate is part of the locking SQL (I-TENANT): rows of another tenant are
        // never locked or read, so a foreign batch is indistinguishable from a missing one.
        var codes = await _repository.GetBatchForUpdateAsync(tenantId.Value, request.BatchId, cancellationToken);
        if (codes.Count == 0)
        {
            return Error.NotFound("Batch.NotFound", $"Access code batch '{request.BatchId}' does not exist.");
        }

        // 3. Evaluate permission if batch is bound to a library
        var libraryId = codes.FirstOrDefault(c => c.LibraryId.HasValue)?.LibraryId;
        if (libraryId.HasValue && _policyEvaluatorService != null)
        {
            var baseContext = _authorizationContextProvider?.CurrentAuthorizationContext ?? new AuthorizationContext
            {
                AuthenticationMethod = "TenantUser"
            };
            var libraryArn = ResourceArn.ForLibrary(tenantId.Value, libraryId.Value);
            var authContext = baseContext.ForResource("library:SellCodes", libraryArn);

            var authResult = await _policyEvaluatorService.Authorize(authContext);
            if (authResult.IsError)
            {
                return authResult.Errors;
            }
        }

        var currentUser = _currentUserRepository != null ? await _currentUserRepository.GetCurrentUser() : null;

        int voidedCount = 0;
        int skippedCount = 0;
        var voidedCodeIds = new List<Guid>();

        // 4. Iterate each code: void voidable codes, skip terminal codes (Voided/Expired)
        foreach (var code in codes)
        {
            if (code.Status == AccessCodeStatus.Voided || code.Status == AccessCodeStatus.Expired)
            {
                skippedCount++;
                continue;
            }

            var voidResult = code.Void(request.Reason, currentUser?.UserId);
            if (voidResult.IsError)
            {
                return voidResult.Errors;
            }

            // Revocation of redeemed codes is producer-only (#58): the voided domain event
            // carries the redeemed user; AccessCodeVoidedDomainEventHandler publishes
            // AccessCodeRevokedIntegrationEvent for downstream revocation. No inline strategy.

            var updateResult = await _repository.UpdateAsync(code, cancellationToken);
            if (updateResult.IsError)
            {
                return updateResult.Errors;
            }

            voidedCount++;
            voidedCodeIds.Add(code.Id);
        }

        _logger.LogInformation(
            "Recalled Batch {BatchId}: Total={Total}, Voided={Voided}, Skipped={Skipped}, Reason='{Reason}'",
            request.BatchId, codes.Count, voidedCount, skippedCount, request.Reason);

        return new VoidBatchResult(request.BatchId, codes.Count, voidedCount, skippedCount, voidedCodeIds);
    }
}

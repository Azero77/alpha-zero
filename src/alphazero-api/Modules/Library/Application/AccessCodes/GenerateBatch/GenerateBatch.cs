using AlphaZero.Modules.Library.Domain;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AlphaZero.Modules.Library.Application.AccessCodes.GenerateBatch;

public record GenerateBatchResult(Guid BatchId, int GeneratedCount, List<string> Codes);

public record GenerateBatchCommand(
    Guid LibraryId,
    int Quantity,
    string StrategyId,
    string TargetResourceArn,
    decimal FaceValue,
    Dictionary<string, object> Metadata) : ICommand<GenerateBatchResult>, ITransactionalCommand;

public class GenerateBatchCommandValidator : AbstractValidator<GenerateBatchCommand>
{
    // Mirrors the Courses provisioning contract's exact key names (CoursePlanSelector
    // reads these case-sensitively). Library only validates reference syntax here;
    // whether the referenced plan exists is decided at redemption — validating
    // existence at mint time would couple Library to the Courses module.
    public const string PlanKey = "plan";
    public const string LegacyPlanKey = "planId";

    public GenerateBatchCommandValidator()
    {
        RuleFor(x => x.LibraryId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(1000);
        RuleFor(x => x.StrategyId).NotEmpty();
        RuleFor(x => x.TargetResourceArn).NotEmpty();
        RuleFor(x => x.FaceValue).GreaterThan(0).LessThanOrEqualTo(1000);

        // Reject at mint: an invalid plan reference mints codes that fail
        // deterministically at redemption (CoursePlan.NotFound) — surfacing
        // the defect only after the batch has been distributed.
        RuleFor(x => x.Metadata)
            .Must(HaveGuidPlanReference)
            .When(x => x.Metadata is not null && x.Metadata.Count is > 0)
            .WithMessage(string.Format(
                "Metadata keys '{0}' or '{1}' must be a valid plan GUID.",
                PlanKey,
                LegacyPlanKey))
            .WithErrorCode("Library.Batch.InvalidMetadata");
    }

    internal static bool HaveGuidPlanReference(Dictionary<string, object>? metadata)
    {
        if (metadata is null)
        {
            return true;
        }

        var planReferences = metadata.Keys.Where(
            key => key is PlanKey or LegacyPlanKey);

        return planReferences.All(key => Guid.TryParse(metadata[key]?.ToString(), out _));
    }
}

public sealed class GenerateBatchCommandHandler : IRequestHandler<GenerateBatchCommand, ErrorOr<GenerateBatchResult>>
{
    private readonly IAccessCodeRepository _accessCodeRepository;
    private readonly ILibraryRepository _libraryRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<GenerateBatchCommandHandler> _logger;
    private readonly ICurrentTenantUserRepository? _currentUserRepository;

    public GenerateBatchCommandHandler(
        IAccessCodeRepository accessCodeRepository,
        ILibraryRepository libraryRepository,
        ITenantProvider tenantProvider,
        IPasswordHasher passwordHasher,
        ILogger<GenerateBatchCommandHandler> logger,
        ICurrentTenantUserRepository? currentUserRepository = null)
    {
        _accessCodeRepository = accessCodeRepository;
        _libraryRepository = libraryRepository;
        _tenantProvider = tenantProvider;
        _passwordHasher = passwordHasher;
        _logger = logger;
        _currentUserRepository = currentUserRepository;
    }

    public async Task<ErrorOr<GenerateBatchResult>> Handle(GenerateBatchCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenant();
        if (tenantId is null) return Error.Unauthorized("Tenant.NotFound", "Tenant not found.");

        // Strict Enforcement: Library must exist and belong to the tenant
        var library = await _libraryRepository.GetById(request.LibraryId);
        if (library is null) return Error.NotFound("Library.NotFound", "Library not found.");

        var resourceArn = ResourceArn.Create(request.TargetResourceArn);
        if (resourceArn.IsError) return resourceArn.Errors;

        // Strict Enforcement: Library must be authorized to sell this resource
        if (!library.AllowedResources.Any(ar => ar.IsMatch(resourceArn.Value)))
        {
            return Error.Forbidden("Library.Batch.Forbidden", "Library is not authorized to sell this resource.", new Dictionary<string, object>()
            {
                {"Allowed" , string.Join(",", library.AllowedResources.Select(a => a.Value)) },
                { "Requested" , request.TargetResourceArn }
            });
        }

        var currentUser = _currentUserRepository != null ? await _currentUserRepository.GetCurrentUser() : null;
        var batchId = Guid.NewGuid();
        var generatedCodes = new List<string>();
        var metadata = new Dictionary<string, object>(request.Metadata ?? new())
        {
            ["FaceValue"] = request.FaceValue
        };
        var jsonMetadata = JsonDocument.Parse(JsonSerializer.Serialize(metadata));

        for (int i = 0; i < request.Quantity; i++)
        {
            var rawCode = CodeGeneratorHelper.GenerateFriendlyCode();
            var hash = _passwordHasher.HashPassword(rawCode);

            var accessCode = AccessCode.Mint(
                hash,
                tenantId.Value,
                request.LibraryId,
                request.StrategyId,
                resourceArn.Value,
                jsonMetadata,
                batchId,
                actorUserId: currentUser?.UserId);

            _accessCodeRepository.Add(accessCode);
            generatedCodes.Add(rawCode);
        }

        _logger.LogInformation("Generated {Quantity} access codes for Library {LibraryId} in Tenant {TenantId}. Batch: {BatchId}",
            request.Quantity, request.LibraryId, tenantId.Value, batchId);

        return new GenerateBatchResult(batchId, generatedCodes.Count, generatedCodes);
    }
}

public static class CodeGeneratorHelper
{
    public static string GenerateFriendlyCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; 
        var bytes = RandomNumberGenerator.GetBytes(12);
        var res = new StringBuilder();
        for (int i = 0; i < 12; i++)
        {
            if (i > 0 && i % 4 == 0) res.Append("-");
            res.Append(chars[bytes[i] % chars.Length]);
        }
        return res.ToString();
    }
}

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

public record GenerateBatchCommand(
    Guid LibraryId,
    int Quantity,
    string StrategyId,
    string TargetResourceArn,
    Dictionary<string, object> Metadata) : ICommand<List<string>>;

public class GenerateBatchCommandValidator : AbstractValidator<GenerateBatchCommand>
{
    public GenerateBatchCommandValidator()
    {
        RuleFor(x => x.LibraryId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(1000);
        RuleFor(x => x.StrategyId).NotEmpty();
        RuleFor(x => x.TargetResourceArn).NotEmpty();
    }
}

public sealed class GenerateBatchCommandHandler : IRequestHandler<GenerateBatchCommand, ErrorOr<List<string>>>
{
    private readonly IAccessCodeRepository _accessCodeRepository;
    private readonly ILibraryRepository _libraryRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<GenerateBatchCommandHandler> _logger;

    public GenerateBatchCommandHandler(
        IAccessCodeRepository accessCodeRepository,
        ILibraryRepository libraryRepository,
        ITenantProvider tenantProvider,
        IPasswordHasher passwordHasher,
        ILogger<GenerateBatchCommandHandler> logger)
    {
        _accessCodeRepository = accessCodeRepository;
        _libraryRepository = libraryRepository;
        _tenantProvider = tenantProvider;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<ErrorOr<List<string>>> Handle(GenerateBatchCommand request, CancellationToken cancellationToken)
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

        var batchId = Guid.NewGuid();
        var generatedCodes = new List<string>();
        var jsonMetadata = JsonDocument.Parse(JsonSerializer.Serialize(request.Metadata));

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
                batchId);

            _accessCodeRepository.Add(accessCode);
            generatedCodes.Add(rawCode);
        }

        _logger.LogInformation("Generated {Quantity} access codes for Library {LibraryId} in Tenant {TenantId}. Batch: {BatchId}",
            request.Quantity, request.LibraryId, tenantId.Value, batchId);

        return generatedCodes;
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

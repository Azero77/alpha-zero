using AlphaZero.Shared.Application;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentValidation;

namespace AlphaZero.Modules.Identity.Application.Auth.Queries.GenerateDocumentToken;

public record GenerateDocumentTokenQuery(string Scope) : IQuery<GenerateDocumentTokenResponse>;

public record GenerateDocumentTokenResponse(string Token);

public class GenerateDocumentTokenQueryValidator : FluentValidation.AbstractValidator<GenerateDocumentTokenQuery>
{
    public GenerateDocumentTokenQueryValidator()
    {
        RuleFor(x => x.Scope)
            .NotEmpty()
            .Matches(@"^[a-zA-Z0-9_-]+/[a-zA-Z0-9_-]+$")
            .WithMessage("Scope must match pattern (e.g., course/courseId)");
    }
}

public class GenerateDocumentTokenQueryHandler : IRequestHandler<GenerateDocumentTokenQuery, ErrorOr<GenerateDocumentTokenResponse>>
{
    private readonly ITenantProvider _tenantProvider;
    private readonly IAuthorizationContextFactory _authorizationContextFactory;
    private readonly IPolicyEvaluatorService _policyEvaluatorService;
    private readonly IConfiguration _configuration;
    private readonly IClock _clock;

    public GenerateDocumentTokenQueryHandler(
        ITenantProvider tenantProvider,
        IAuthorizationContextFactory authorizationContextFactory,
        IPolicyEvaluatorService policyEvaluatorService,
        IConfiguration configuration,
        IClock clock)
    {
        _tenantProvider = tenantProvider;
        _authorizationContextFactory = authorizationContextFactory;
        _policyEvaluatorService = policyEvaluatorService;
        _configuration = configuration;
        _clock = clock;
    }

    public async Task<ErrorOr<GenerateDocumentTokenResponse>> Handle(GenerateDocumentTokenQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenant();
        if (tenantId is null)
            return Error.Unauthorized("Tenant.NotFound", "Active tenant is required.");

        var scope = request.Scope.Trim('/');
        var parts = scope.Split('/');
        if (parts.Length != 2)
            return Error.Validation("Scope.Invalid", "Scope must follow the format 'type/id'.");

        var service = parts[0].ToLowerInvariant();
        var arnResult = ResourceArn.Create(service, tenantId.Value.ToString(), scope);

        if (arnResult.IsError)
            return arnResult.Errors;

        var contextResult = await _authorizationContextFactory.Create(
            "content:read", 
            arnResult.Value, 
            AuthenticationMethod.TenantUser, 
            arnResult.Value.Value, 
            cancellationToken);

        if (contextResult.IsError)
            return contextResult.Errors;

        var authResult = await _policyEvaluatorService.Authorize(contextResult.Value);
        
        if (authResult.IsError)
        {
            // If authorization fails, return Forbidden
            return Error.Forbidden("Access.Denied", "You do not have permission to access this document scope.");
        }

        var secret = _configuration["DOCUMENT_HMAC_SECRET"];
        if (string.IsNullOrEmpty(secret))
        {
            return Error.Unexpected("Config.Missing", "Document HMAC secret is not configured.");
        }

        var exp = new DateTimeOffset(_clock.Now.AddHours(1)).ToUnixTimeSeconds();
        
        var payload = new
        {
            path = $"/documents/{tenantId.Value}/{scope}/",
            exp = exp
        };

        var json = JsonSerializer.Serialize(payload);
        var base64Payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(json))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var signatureHex = GenerateHmacSha256Hex(base64Payload, secret);
        var token = $"{base64Payload}.{signatureHex}";

        return new GenerateDocumentTokenResponse(token);
    }

    private static string GenerateHmacSha256Hex(string text, string key)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var textBytes = Encoding.UTF8.GetBytes(text);
        
        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(textBytes);
        
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}

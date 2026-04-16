using AlphaZero.Modules.Identity.Application.Auth.Commands.LoginAsTenantUser;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Identity.Presentation.Auth.Commands.LoginAsTenantUser;

public record LoginAsTenantUserRequest
{
    public Guid TenantId { get; init; }
}

public class LoginAsTenantUserEndpoint : Endpoint<LoginAsTenantUserRequest, TokenResponse>
{
    private readonly IdentityModule _module;

    public LoginAsTenantUserEndpoint(IdentityModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Post("/identity/auth/exchange-tenant-token");
        // This endpoint requires the GLOBAL Cognito JWT
        // It will be validated by the standard ASP.NET Core Authentication middleware
        // which we will configure for Cognito
        Description(d => d.WithTags("Identity Auth"));
    }

    public override async Task HandleAsync(LoginAsTenantUserRequest req, CancellationToken ct)
    {
        // Extract the sub claim from the Cognito JWT
        var identityId = User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        var name = User.Claims.FirstOrDefault(c => c.Type == "name")?.Value ?? "User";

        if (string.IsNullOrEmpty(identityId))
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        var command = new LoginAsTenantUserCommand(identityId, req.TenantId, name);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}

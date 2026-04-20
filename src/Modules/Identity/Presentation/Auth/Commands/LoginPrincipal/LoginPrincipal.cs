using AlphaZero.Modules.Identity.Application.Auth.Commands.LoginAsTenantUser;
using AlphaZero.Modules.Identity.Application.Auth.Commands.LoginPrincipal;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Identity.Presentation.Auth.Commands.LoginPrincipal;

public record LoginPrincipalRequest
{
    public Guid TenantId { get; init; }
    public string Username { get; init; } = default!;
    public string Password { get; init; } = default!;
}

public class LoginPrincipalEndpoint : Endpoint<LoginPrincipalRequest, TokenResponse>
{
    private readonly IdentityModule _module;

    public LoginPrincipalEndpoint(IdentityModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Post("/identity/auth/login-principal");
        AllowAnonymous();
        Description(d => d.WithTags("Identity Auth"));
    }

    public override async Task HandleAsync(LoginPrincipalRequest req, CancellationToken ct)
    {
        var command = new LoginPrincipalCommand(req.TenantId, req.Username, req.Password);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}

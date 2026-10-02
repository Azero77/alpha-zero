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

public class LoginPrincipalSummary : Summary<LoginPrincipalEndpoint>
{
    public LoginPrincipalSummary()
    {
        Summary = "Authenticates a principal";
        Description = "Validates principal credentials and returns an access token.";
        Response<TokenResponse>(200, "Authentication successful");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Invalid credentials (Auth.NotFoundCredentials, Auth.InvalidCredentials)");
    }
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
        Summary(new LoginPrincipalSummary());
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

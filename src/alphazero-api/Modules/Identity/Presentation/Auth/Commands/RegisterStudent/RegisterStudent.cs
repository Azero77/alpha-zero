using AlphaZero.Modules.Identity.Application.Auth.Commands.RegisterStudent;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Identity.Presentation.Auth.Commands.RegisterStudent;

public record RegisterStudentRequest
{
    public Guid TenantId { get; init; }
    public string Username { get; init; } = default!;
    public string Password { get; init; } = default!;
    public string Name { get; init; } = default!;
}

public record RegisterStudentResponse(Guid Id);

public class RegisterStudentSummary : Summary<RegisterStudentEndpoint>
{
    public RegisterStudentSummary()
    {
        Summary = "Registers a new student";
        Description = "Creates a student principal associated with a tenant.";
        Response<RegisterStudentResponse>(200, "Student successfully registered");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (TenantId empty, Username empty/too long, Password < 8 chars, Name empty/too long)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "StudentAccess policy not found (ManagedPolicy.NotFound)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(409, "User already exists");
    }
}

public class RegisterStudentEndpoint : Endpoint<RegisterStudentRequest, RegisterStudentResponse>
{
    private readonly IdentityModule _module;

    public RegisterStudentEndpoint(IdentityModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Post("/identity/auth/register-student");
        AllowAnonymous();
        Description(d => d.WithTags("Identity Auth"));
        Summary(new RegisterStudentSummary());
    }

    public override async Task HandleAsync(RegisterStudentRequest req, CancellationToken ct)
    {
        var command = new RegisterStudentCommand(req.TenantId, req.Username, req.Password, req.Name);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(new RegisterStudentResponse(result.Value), ct);
    }
}

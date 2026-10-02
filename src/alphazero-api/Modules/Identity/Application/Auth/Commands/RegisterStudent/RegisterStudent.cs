using AlphaZero.Modules.Identity.Domain.Models.Principals;
using AlphaZero.Modules.Identity.Domain.Repositories;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Identity.Application.Auth.Commands.RegisterStudent;

public record RegisterStudentCommand(
    Guid TenantId,
    string Username,
    string Password,
    string Name) : ICommand<Guid>;

public class RegisterStudentCommandValidator : AbstractValidator<RegisterStudentCommand>
{
    public RegisterStudentCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Username).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public sealed class RegisterStudentCommandHandler : IRequestHandler<RegisterStudentCommand, ErrorOr<Guid>>
{
    private readonly IPrincipalRepository _principalRepository;
    private readonly IManagedPolicyRepository _managedPolicyRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<RegisterStudentCommandHandler> _logger;

    public RegisterStudentCommandHandler(
        IPrincipalRepository principalRepository,
        IManagedPolicyRepository managedPolicyRepository,
        IPasswordHasher passwordHasher,
        ILogger<RegisterStudentCommandHandler> _logger)
    {
        _principalRepository = principalRepository;
        _managedPolicyRepository = managedPolicyRepository;
        _passwordHasher = passwordHasher;
        this._logger = _logger;
    }

    public async Task<ErrorOr<Guid>> Handle(RegisterStudentCommand request, CancellationToken cancellationToken)
    {
        // 1. Check if principal with username already exists in this tenant
        if (await _principalRepository.Any(p => p.Username == request.Username && p.TenantId == request.TenantId, cancellationToken))
        {
            return Error.Conflict("Principal.DuplicateUsername", $"A principal with the username '{request.Username}' already exists in this tenant.");
        }

        // 2. Fetch the StudentAccess managed policy
        var studentPolicy = await _managedPolicyRepository.GetFirst(p => p.Name == "StudentAccess", cancellationToken);
        if (studentPolicy is null)
        {
            // Try by ID fallback
            studentPolicy = await _managedPolicyRepository.GetById(Guid.Parse("00000000-0000-0000-0000-000000000003"), cancellationToken);
        }

        if (studentPolicy is null)
        {
            return Error.NotFound("ManagedPolicy.NotFound", "StudentAccess policy not found.");
        }

        // 3. Hash the password
        var passwordHash = _passwordHasher.HashPassword(request.Password);

        // 4. Create the Principal User
        var principalId = Guid.NewGuid();
        var principalResult = Principal.Create(
            principalId,
            request.Username,
            passwordHash,
            request.Name,
            PrincipalType.User,
            null, // principalScope
            request.TenantId);

        if (principalResult.IsError)
        {
            return principalResult.Errors;
        }

        var principal = principalResult.Value;

        // 5. Attach the StudentAccess policy
        principal.AddPolicy(studentPolicy);

        // 6. Save the principal
        _principalRepository.Add(principal);

        _logger.LogInformation("Student principal {PrincipalId} (Username: {Username}) registered in Tenant {TenantId}.",
            principalId, request.Username, request.TenantId);

        return principalId;
    }
}

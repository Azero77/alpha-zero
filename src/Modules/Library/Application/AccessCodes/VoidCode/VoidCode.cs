using AlphaZero.Modules.Library.Domain;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Domain;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Library.Application.AccessCodes.VoidCode;

public record VoidCodeCommand(string RawCode, string Reason) : ICommand<Success>;

public class VoidCodeCommandValidator : AbstractValidator<VoidCodeCommand>
{
    public VoidCodeCommandValidator()
    {
        RuleFor(x => x.RawCode).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(512);
    }
}

public sealed class VoidCodeCommandHandler(
    IAccessCodeRepository repository,
    IPasswordHasher passwordHasher,
    ILogger<VoidCodeCommandHandler> logger) : IRequestHandler<VoidCodeCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(VoidCodeCommand request, CancellationToken cancellationToken)
    {
        var hash = passwordHasher.HashPassword(request.RawCode);
        var code = await repository.GetByHashAsync(hash, cancellationToken);

        if (code is null)
        {
            return Error.NotFound("AccessCode.NotFound", "The provided code is invalid.");
        }

        var result = code.Void(request.Reason);
        if (result.IsError) return result.Errors;

        repository.Update(code);
        logger.LogWarning("Access Code {CodeId} has been VOIDED. Reason: {Reason}", code.Id, request.Reason);

        return Result.Success;
    }
}

using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Subject;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Application.Subjects.Commands.Create;

public record CreateSubjectCommand(string Name, string? Description) : ICommand<Guid>;

public class CreateSubjectCommandValidator : AbstractValidator<CreateSubjectCommand>
{
    public CreateSubjectCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public sealed class CreateSubjectCommandHandler : IRequestHandler<CreateSubjectCommand, ErrorOr<Guid>>
{
    private readonly ISubjectRepository _subjectRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<CreateSubjectCommandHandler> _logger;

    public CreateSubjectCommandHandler(
        ISubjectRepository subjectRepository,
        ITenantProvider tenantProvider,
        ILogger<CreateSubjectCommandHandler> logger)
    {
        _subjectRepository = subjectRepository;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    public async Task<ErrorOr<Guid>> Handle(CreateSubjectCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenant();
        if (tenantId is null)
        {
            return Error.Unauthorized("Tenant.NotFound", "Tenant could not be determined.");
        }

        var subjectId = Guid.NewGuid();
        var subjectResult = Subject.Create(subjectId, tenantId.Value, request.Name, request.Description);

        if (subjectResult.IsError)
        {
            return subjectResult.Errors;
        }

        _subjectRepository.Add(subjectResult.Value);
        _logger.LogInformation("Subject {SubjectId} created for Tenant {TenantId}", subjectId, tenantId.Value);

        return subjectId;
        // Changes are saved by the UnitOfWork pipeline behavior
    }
}

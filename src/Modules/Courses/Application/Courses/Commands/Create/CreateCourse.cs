using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Shared.Application;
using AlphaZero.Shared.Infrastructure.Tenats;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AlphaZero.Modules.Courses.Application.Courses.Commands.Create;

public record CreateCourseCommand(string Title, string? Description, Guid SubjectId) : ICommand<Guid>;

public class CreateCourseCommandValidator : AbstractValidator<CreateCourseCommand>
{
    public CreateCourseCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(255);
        RuleFor(x => x.SubjectId).NotEmpty();
    }
}

public sealed class CreateCourseCommandHandler : IRequestHandler<CreateCourseCommand, ErrorOr<Guid>>
{
    private readonly ICourseRepository _courseRepository;
    private readonly ISubjectRepository _subjectRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<CreateCourseCommandHandler> _logger;

    public CreateCourseCommandHandler(
        ICourseRepository courseRepository,
        ISubjectRepository subjectRepository,
        ITenantProvider tenantProvider,
        ILogger<CreateCourseCommandHandler> logger)
    {
        _courseRepository = courseRepository;
        _subjectRepository = subjectRepository;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    public async Task<ErrorOr<Guid>> Handle(CreateCourseCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenant();
        if (tenantId is null) return Error.Unauthorized("Tenant.NotFound", "Tenant not found.");

        var subjectExists = await _subjectRepository.Any(s => s.Id == request.SubjectId, cancellationToken);
        if (!subjectExists) return Error.NotFound("Course.SubjectId", "Provided SubjectId does not exist.");

        var courseId = Guid.NewGuid();
        var courseResult = Course.Create(courseId, tenantId.Value, request.Title, request.Description, request.SubjectId);

        if (courseResult.IsError) return courseResult.Errors;

        _courseRepository.Add(courseResult.Value);
        _logger.LogInformation("Course {CourseId} created.", courseId);

        return courseId;
    }
}

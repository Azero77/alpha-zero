using AlphaZero.Modules.Courses.Application.Repositories;
using AlphaZero.Shared.Application;
using ErrorOr;
using MediatR;

namespace AlphaZero.Modules.Courses.Application.Courses.Commands.Assets;

public record UnassignAssetFromCurriculumCommand(
    Guid CourseId,
    Guid ItemId
) : ICommand<Success>;

public sealed class UnassignAssetFromCurriculumCommandHandler 
    : IRequestHandler<UnassignAssetFromCurriculumCommand, ErrorOr<Success>>
{
    private readonly ICourseRepository _courseRepository;

    public UnassignAssetFromCurriculumCommandHandler(ICourseRepository courseRepository)
        => _courseRepository = courseRepository;

    public async Task<ErrorOr<Success>> Handle(
        UnassignAssetFromCurriculumCommand request, CancellationToken ct)
    {
        var course = await _courseRepository.GetByIdWithSectionsAndAssetsAsync(request.CourseId, ct);
        if (course is null) return Error.NotFound("Course.NotFound");

        var result = course.UnassignFromCurriculum(request.ItemId);
        if (result.IsError) return result.Errors;

        _courseRepository.Update(course);
        return Result.Success;
    }
}

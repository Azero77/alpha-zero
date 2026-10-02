using AlphaZero.Modules.Assessments.IntegrationEvents;
using ErrorOr;

namespace AlphaZero.Modules.Courses.Application.Services;

public interface IAssessmentService
{
    Task<ErrorOr<AssessmentCreatedResponse>> AddAssessment(CreateAssessmentRequest request);
}

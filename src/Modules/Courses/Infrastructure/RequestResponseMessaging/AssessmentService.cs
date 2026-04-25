using AlphaZero.Modules.Assessments.IntegrationEvents;
using AlphaZero.Modules.Courses.Application.Courses.Commands.AddAssessment;
using ErrorOr;
using MassTransit;

namespace AlphaZero.Modules.Courses.Infrastructure.RequestResponseMessaging;

public class AssessmentService : IAssessmentService
{
    private readonly IRequestClient<CreateAssessmentRequest> _requestClient;

    public AssessmentService(IRequestClient<CreateAssessmentRequest> requestClient)
    {
        _requestClient = requestClient;
    }

    public async Task<ErrorOr<AssessmentCreatedResponse>> AddAssessment(CreateAssessmentRequest request)
    {
        var response = await _requestClient.GetResponse<AssessmentCreatedResponse, AssessmentCreationFailedResponse>(request);
        if (response.Is<AssessmentCreatedResponse>(out var success))
        {
            return success.Message;
        }
        else if (response.Is<AssessmentCreationFailedResponse>(out var failure))
        {
            return Error.Failure(failure.Message.Reason);
        }
        return Error.Failure("Unknown error");
    }
}

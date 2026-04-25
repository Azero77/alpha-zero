using AlphaZero.Modules.Assessments.Application.Assessments.Commands.Create;
using AlphaZero.Modules.Assessments.Domain.Enums;
using AlphaZero.Modules.Assessments.IntegrationEvents;
using MassTransit;
using MediatR;

namespace AlphaZero.Modules.Assessments.Infrastructure.Consumers;

public class CreateAssessmentRequestHandler(IMediator mediator) : IConsumer<CreateAssessmentRequest>
{
    public async Task Consume(ConsumeContext<CreateAssessmentRequest> context)
    {
        
        if(!Enum.TryParse<AssessmentType>(context.Message.Type, out var assessmentType))
        {
            await context.RespondAsync(new AssessmentCreationFailedResponse
            (
               $"Invalid assessment type: {context.Message.Type}"
            ));

            return;
        }
        var request = new CreateAssessmentCommand(context.Message.Title,context.Message.Description,assessmentType, context.Message.PassingScore);

        var response = await mediator.Send(request);
        if(response.IsError)
        {
            await context.RespondAsync(new AssessmentCreationFailedResponse
            (
               response.Errors.First().Description
            ));
            return;
        }
        await context.RespondAsync(new AssessmentCreatedResponse(response.Value));
    }
}

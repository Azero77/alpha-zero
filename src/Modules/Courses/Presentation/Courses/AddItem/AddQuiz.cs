using AlphaZero.Modules.Courses.Application.Courses.Commands.AddQuiz;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Courses.AddItem;

public record AddQuizRequest
{
    public Guid CourseId { get; init; }
    public Guid SectionId { get; init; }
    public string Title { get; init; } = default!;
    public Guid QuizId { get; init; }
}

public class AddQuizEndpoint : Endpoint<AddQuizRequest>
{
    private readonly CoursesModule _module;

    public AddQuizEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Post("/courses/{CourseId}/sections/{SectionId}/quizzes");
        AllowAnonymous();
        Description(d => d.WithTags("Courses"));
    }

    public override async Task HandleAsync(AddQuizRequest req, CancellationToken ct)
    {
        var command = new AddQuizCommand(req.CourseId, req.SectionId, req.Title, req.QuizId);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}

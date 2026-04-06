using AlphaZero.Modules.Courses.Application.Enrollements.Commands.CompleteItem;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Enrollements.CompleteItem;

public record CompleteItemRequest
{
    public Guid EnrollmentId { get; init; }
    public int BitIndex { get; init; }
}

public class CompleteItemEndpoint : Endpoint<CompleteItemRequest>
{
    private readonly CoursesModule _module;

    public CompleteItemEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Post("/courses/enrollements/{EnrollmentId}/complete");
        AllowAnonymous();
        Description(d => d.WithTags("Enrollements"));
    }

    public override async Task HandleAsync(CompleteItemRequest req, CancellationToken ct)
    {
        var command = new CompleteItemCommand(req.EnrollmentId, req.BitIndex);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}

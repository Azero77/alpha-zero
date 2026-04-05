using AlphaZero.Modules.Courses.Application.Subjects.Commands.Create;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Subjects.Create;

public record CreateSubjectRequest
{
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
}

public record CreateSubjectResponse(Guid Id);

public class CreateSubjectSummary : Summary<CreateSubjectEndpoint>
{
    public CreateSubjectSummary()
    {
        Summary = "Creates a new educational subject";
        Description = "Initializes a subject category (e.g., Physics, Chemistry) for the current tenant.";
        ExampleRequest = new CreateSubjectRequest
        {
            Name = "Mathematics",
            Description = "General mathematics curriculum for high school."
        };
        Response<CreateSubjectResponse>(201, "Subject successfully created");
        Response(400, "Validation failure");
        Response(401, "Tenant context missing");
    }
}

public class CreateSubjectEndpoint : Endpoint<CreateSubjectRequest, CreateSubjectResponse>
{
    private readonly CoursesModule _module;

    public CreateSubjectEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Post("/courses/subjects");
        AllowAnonymous();
        Description(d => d.WithTags("Subjects"));
        Summary(new CreateSubjectSummary());
    }
    public override async Task HandleAsync(CreateSubjectRequest req, CancellationToken ct)
    {
        var command = new CreateSubjectCommand(req.Name, req.Description);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(new CreateSubjectResponse(result.Value), ct);
    }
}

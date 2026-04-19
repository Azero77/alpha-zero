using AlphaZero.Modules.Courses.Application.Courses.Commands.Create;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using Amazon.S3;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Courses.Create;

public record CreateCourseRequest
{
    public string Title { get; init; } = default!;
    public string? Description { get; init; }
    public Guid SubjectId { get; init; }
}

public record CreateCourseResponse(Guid Id);

public class CreateCourseSummary : Summary<CreateCourseEndpoint>
{
    public CreateCourseSummary()
    {
        Summary = "Initializes a new course";
        Description = "Creates a course in Draft status under a specific subject.";
        ExampleRequest = new CreateCourseRequest
        {
            Title = "Introduction to Algebra",
            Description = "A basic course covering algebraic foundations.",
            SubjectId = Guid.Parse("00000000-0000-0000-0000-000000000001")
        };
        Response<CreateCourseResponse>(201, "Course successfully created");
        Response(400, "Validation failure");
        Response(404, "Subject not found");
    }
}

public class CreateCourseEndpoint : Endpoint<CreateCourseRequest, CreateCourseResponse>
{
    private readonly CoursesModule _module;

    public CreateCourseEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Post("/courses");
        this.AccessControl("courses:Create", _ => ResourceArn.ForTenant(Guid.Empty));
        Description(d => d.WithTags("Courses"));
        Summary(new CreateCourseSummary());
    }
    public override async Task HandleAsync(CreateCourseRequest req, CancellationToken ct)
    {
        var command = new CreateCourseCommand(req.Title, req.Description, req.SubjectId);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.CreatedAtAsync($"courses/{result.Value}", responseBody: new CreateCourseResponse(result.Value), cancellation : ct);
    }
}

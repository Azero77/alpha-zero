using AlphaZero.Modules.Courses.Application.Subjects.Commands.Update;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Subjects.Update;

public record UpdateSubjectRequest
{
    public Guid Id { get; init; }
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
}

public class UpdateSubjectSummary : Summary<UpdateSubjectEndpoint>
{
    public UpdateSubjectSummary()
    {
        Summary = "Renames a subject";
        Description = "Updates the subject (category) name and description; every course referencing the subject keeps selling under its new label.";
        ExampleRequest = new UpdateSubjectRequest
        {
            Id = Guid.NewGuid(),
            Name = "Applied Mathematics",
            Description = "Renamed category."
        };
        Response(204, "Subject updated successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (name empty/too long)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized (Tenant.NotFound)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing subjects:Edit permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Subject not found (Subject.NotFound)");
    }
}

public class UpdateSubjectEndpoint : Endpoint<UpdateSubjectRequest>
{
    private readonly CoursesModule _module;

    public UpdateSubjectEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Put("/courses/subjects/{Id}");
        this.AccessControl("subjects:Edit", (req, tenantId) => ResourceArn.ForTenant(tenantId));
        Description(d => d
            .WithTags("Subjects")
            .Accepts<UpdateSubjectRequest>("application/json"));
        Summary(new UpdateSubjectSummary());
    }

    public override async Task HandleAsync(UpdateSubjectRequest req, CancellationToken ct)
    {
        var command = new UpdateSubjectCommand(req.Id, req.Name, req.Description);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}

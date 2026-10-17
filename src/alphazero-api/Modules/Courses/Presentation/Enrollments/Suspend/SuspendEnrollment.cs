using AlphaZero.Modules.Courses.Application.Enrollments.Commands.Suspend;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Enrollments.Suspend;

public record SuspendEnrollmentRequest
{
    public Guid Id { get; init; }
    public string? Reason { get; init; }
}

public class SuspendEnrollmentSummary : Summary<SuspendEnrollmentEndpoint>
{
    public SuspendEnrollmentSummary()
    {
        Summary = "Suspends an enrollment";
        Description = "Support/billing operation to suspend a student's enrollment (distinct from deactivation — reversible pause of access, e.g. while a billing dispute resolves). Idempotent — suspending an already-suspended enrollment succeeds. Completed progress is retained.";
        ExampleRequest = new SuspendEnrollmentRequest { Id = Guid.NewGuid(), Reason = "Billing dispute" };
        Response(204, "Enrollment suspended successfully (also returned when it was already suspended)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (id empty)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized (Tenant.NotFound)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing courses:Edit permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Enrollment not found (Enrollment.NotFound)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(409, "Invalid transition (Enrollment.InvalidTransition)");
    }
}

public class SuspendEnrollmentEndpoint : Endpoint<SuspendEnrollmentRequest>
{
    private readonly CoursesModule _module;

    public SuspendEnrollmentEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Patch("/courses/enrollments/{Id}/suspend");
        this.AccessControl("courses:Edit", (req, tenantId) => ResourceArn.ForEnrollment(tenantId, req.Id));
        Description(d => d.WithTags("Enrollment"));
        Summary(new SuspendEnrollmentSummary());
    }

    public override async Task HandleAsync(SuspendEnrollmentRequest req, CancellationToken ct)
    {
        var command = new SuspendEnrollmentCommand(req.Id, req.Reason);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}

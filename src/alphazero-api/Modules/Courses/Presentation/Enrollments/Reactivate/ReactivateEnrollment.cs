using AlphaZero.Modules.Courses.Application.Enrollments.Commands.Reactivate;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Enrollments.Reactivate;

public record ReactivateEnrollmentRequest
{
    public Guid Id { get; init; }
}

public class ReactivateEnrollmentSummary : Summary<ReactivateEnrollmentEndpoint>
{
    public ReactivateEnrollmentSummary()
    {
        Summary = "Reactivates an enrollment";
        Description = "Support/admin operation to reactivate a suspended, deactivated, or expired enrollment so a resumed payment or resolved dispute restores the student. Progress earned before the transition is preserved; therefore re-activation only flips status. Idempotent — reactivating an already-active enrollment succeeds.";
        ExampleRequest = new ReactivateEnrollmentRequest { Id = Guid.NewGuid() };
        Response(204, "Enrollment reactivated successfully (also returned when it was already active)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (id empty)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized (Tenant.NotFound)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing courses:Edit permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Enrollment not found (Enrollment.NotFound)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(409, "Invalid transition (Enrollment.InvalidTransition)");
    }
}

public class ReactivateEnrollmentEndpoint : Endpoint<ReactivateEnrollmentRequest>
{
    private readonly CoursesModule _module;

    public ReactivateEnrollmentEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Patch("/courses/enrollments/{Id}/reactivate");
        this.AccessControl("courses:Edit", (req, tenantId) => ResourceArn.ForEnrollment(tenantId, req.Id));
        Description(d => d.WithTags("Enrollment"));
        Summary(new ReactivateEnrollmentSummary());
    }

    public override async Task HandleAsync(ReactivateEnrollmentRequest req, CancellationToken ct)
    {
        var command = new ReactivateEnrollmentCommand(req.Id);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}

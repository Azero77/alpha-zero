using AlphaZero.Modules.Courses.Application.Enrollments.Commands.Deactivate;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Enrollments.Deactivate;

public record DeactivateEnrollmentRequest
{
    public Guid Id { get; init; }
}

public class DeactivateEnrollmentSummary : Summary<DeactivateEnrollmentEndpoint>
{
    public DeactivateEnrollmentSummary()
    {
        Summary = "Deactivates an enrollment";
        Description = "Support/admin operation to deactivate a student's enrollment (idempotent — deactivating an already-inactive enrollment succeeds). Completed progress is retained for later reactivation.";
        ExampleRequest = new DeactivateEnrollmentRequest { Id = Guid.NewGuid() };
        Response(204, "Enrollment deactivated successfully (also returned when it was already inactive)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(400, "Validation failure (id empty)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized (Tenant.NotFound)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing courses:Edit permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Enrollment not found (Enrollment.NotFound)");
    }
}

public class DeactivateEnrollmentEndpoint : Endpoint<DeactivateEnrollmentRequest>
{
    private readonly CoursesModule _module;

    public DeactivateEnrollmentEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Post("/courses/enrollments/{Id}/deactivate");
        this.AccessControl("courses:Edit", (req, tenantId) => ResourceArn.ForEnrollment(tenantId, req.Id));
        Description(d => d.WithTags("Enrollment"));
        Summary(new DeactivateEnrollmentSummary());
    }

    public override async Task HandleAsync(DeactivateEnrollmentRequest req, CancellationToken ct)
    {
        var command = new DeactivateEnrollmentByIdCommand(req.Id);
        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}

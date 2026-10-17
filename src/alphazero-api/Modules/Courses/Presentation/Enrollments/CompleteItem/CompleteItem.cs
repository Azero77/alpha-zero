using AlphaZero.Modules.Courses.Application.Enrollments.Commands.CompleteItem;
using AlphaZero.Modules.Courses.Presentation;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Enrollments.CompleteItem;

public record CompleteItemRequest
{
    public Guid EnrollmentId { get; init; }
    public int BitIndex { get; init; }
}

public class CompleteItemSummary : Summary<CompleteItemEndpoint>
{
    public CompleteItemSummary()
    {
        Summary = "Marks a course item as completed";
        Description = "Updates the student's progress bitmask for the specified item bit index.";
        Response(204, "Item marked as completed successfully");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing enrollments:Complete permission)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Not found (Enrollment.NotFound, Course.Item)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(409, "Conflict (Enrollment.Status - cannot complete items in inactive enrollment)");
    }
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
        Post("/courses/enrollments/{EnrollmentId}/complete");
        this.AccessControl("enrollments:Complete", (req, tenantId) => ResourceArn.ForEnrollment(tenantId, req.EnrollmentId));
        Description(d => d.WithTags("Enrollment"));
        Summary(new CompleteItemSummary());
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

/// <summary>
/// ⚠️ TEMPORARY legacy alias (#31): already-deployed frontend builds still call the misspelled
/// '/courses/enrollements/{id}/complete' route. This alias serves identical behavior while those
/// clients roll over to the correctly-spelled primary route above. Scheduled for removal next
/// release cycle — do not add new consumers of the legacy spelling.
/// </summary>
public class CompleteItemLegacyAliasEndpoint : CompleteItemEndpoint
{
    public CompleteItemLegacyAliasEndpoint(CoursesModule module) : base(module)
    {
    }

    public override void Configure()
    {
        Post("/courses/enrollements/{EnrollmentId}/complete");
        this.AccessControl("enrollments:Complete", (req, tenantId) => ResourceArn.ForEnrollment(tenantId, req.EnrollmentId));
        Description(d => d.WithTags("Enrollment"));
    }
}

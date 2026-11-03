using AlphaZero.Modules.Courses.Application.Courses.Commands.AccessTickets;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Courses.AccessTickets;

public record IssueAccessTicketRequest
{
    public Guid CourseId { get; init; }
}

public record IssueAccessTicketResponse(string Ticket, int ExpiresInSeconds);

public class IssueAccessTicketSummary : Summary<IssueAccessTicketEndpoint>
{
    public IssueAccessTicketSummary()
    {
        Summary = "Issues a short-lived access-window ticket for course access";
        Description = "Returns a short-lived HMAC-signed access ticket proving the caller's enrollment is active and within its access window. Access is gated statefully at this endpoint; downstream requests present the ticket in the X-Access-Ticket header for stateless evaluation.";
        Response<IssueAccessTicketResponse>(200, "Access ticket successfully issued");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(401, "Unauthorized");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(403, "Forbidden (Missing courses:View permission on the course)");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(404, "Enrollment or course not found");
        Response<Microsoft.AspNetCore.Mvc.ProblemDetails>(409, "Conflict (Enrollment suspended, expired, or access window elapsed)");
    }
}

public class IssueAccessTicketEndpoint : Endpoint<IssueAccessTicketRequest, IssueAccessTicketResponse>
{
    private readonly CoursesModule _module;

    public IssueAccessTicketEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Post("/courses/{CourseId:guid}/access-tickets");
        this.AccessControl("courses:View", (req, tenantId) => ResourceArn.ForCourse(tenantId, req.CourseId));
        Description(d => d.WithTags("Courses"));
        Summary(new IssueAccessTicketSummary());
    }

    public override async Task HandleAsync(IssueAccessTicketRequest req, CancellationToken ct)
    {
        var tenantUser = User.ToTenantUserDTO();
        var studentId = tenantUser?.UserId;

        if (studentId is null || studentId == Guid.Empty)
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var command = new IssueAccessTicketCommand(req.CourseId, studentId.Value);

        var result = await _module.Send(command, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(new IssueAccessTicketResponse(
            result.Value.Ticket,
            result.Value.ExpiresInSeconds), ct);
    }
}

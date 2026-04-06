using AlphaZero.Modules.Courses.Application.Subjects.Queries.GetSubject;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace AlphaZero.Modules.Courses.Presentation.Subjects.Get;

public record GetSubjectRequest
{
    public Guid Id { get; init; }
}

public class GetSubjectEndpoint : Endpoint<GetSubjectRequest, SubjectDto>
{
    private readonly CoursesModule _module;

    public GetSubjectEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Get("/courses/subjects/{id}");
        AllowAnonymous();
        Description(d => d.WithTags("Subjects"));
    }

    public override async Task HandleAsync(GetSubjectRequest req, CancellationToken ct)
    {
        var query = new GetSubjectQuery(req.Id);
        var result = await _module.Send(query, ct);

        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}

using AlphaZero.Modules.Courses.Application.Courses.Commands.UpsertOverview;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace AlphaZero.Modules.Courses.Presentation.Courses.Overview;

public class UpsertCourseOverviewRequest
{
    public Guid Id { get; set; } // bound from route /courses/{Id}/overview
    public JsonElement DescriptionContent { get; set; }
    public JsonElement? TargetAudienceContent { get; set; }
    public JsonElement? LearningObjectivesContent { get; set; }
}

public class UpsertCourseOverviewValidator : Validator<UpsertCourseOverviewRequest>
{
    public UpsertCourseOverviewValidator()
    {
        RuleFor(x => x.DescriptionContent)
            .Must(IsValidJsonBlock)
            .WithMessage("DescriptionContent must be a valid JSON block.");

        RuleFor(x => x.TargetAudienceContent)
            .Must(x => !x.HasValue || IsValidJsonBlock(x.Value))
            .WithMessage("TargetAudienceContent must be a valid JSON block.");

        RuleFor(x => x.LearningObjectivesContent)
            .Must(x => !x.HasValue || IsValidJsonBlock(x.Value))
            .WithMessage("LearningObjectivesContent must be a valid JSON block.");
    }

    private bool IsValidJsonBlock(JsonElement element)
    {
        // Must be an object, usually TipTap/ProseMirror has a "type": "doc" or similar.
        // For ticket 20, just ensuring it's an object is sufficient as basic validation.
        return element.ValueKind == JsonValueKind.Object;
    }
}

public class UpsertCourseOverviewEndpoint : Endpoint<UpsertCourseOverviewRequest>
{
    private readonly CoursesModule _module;

    public UpsertCourseOverviewEndpoint(CoursesModule module)
    {
        _module = module;
    }

    public override void Configure()
    {
        Put("/courses/{Id}/overview");
        this.AccessControl("courses.overview:Manage", (req, tenantId) => ResourceArn.ForCourse(tenantId, req.Id));
        Description(d => d.WithTags("Courses"));
        Summary(s =>
        {
            s.Summary = "Create or update a course overview";
            s.Description = "Upserts the rich text blocks for description, target audience, and learning objectives.";
            s.Responses[200] = "Successfully upserted course overview";
            s.Responses[404] = "Course not found";
        });
    }

    public override async Task HandleAsync(UpsertCourseOverviewRequest req, CancellationToken ct)
    {
        var command = new UpsertCourseOverviewCommand(
            req.Id, 
            req.DescriptionContent, 
            req.TargetAudienceContent, 
            req.LearningObjectivesContent);
            
        var result = await _module.Send(command, ct);
        
        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(ct);
    }
}

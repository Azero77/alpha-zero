using AlphaZero.Modules.Courses.Application.Courses.Commands.UpsertOverview;
using AlphaZero.Shared.Authorization;
using AlphaZero.Shared.Domain;
using AlphaZero.Shared.Presentation.Extensions;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using System.Text.Json;

namespace AlphaZero.Modules.Courses.Presentation.Courses.Overview;

public class UpsertCourseOverviewRequest
{
    public Guid Id { get; set; } // bound from route /courses/{Id}/overview
    public JsonElement DescriptionContent { get; set; }
    public JsonElement? TargetAudienceContent { get; set; }
    public JsonElement? LearningObjectivesContent { get; set; }
}

public static class ValidatorExtensions 
{
    public static IRuleBuilderOptions<T, JsonElement> MustBeValidJsonBlock<T>(this IRuleBuilder<T, JsonElement> ruleBuilder)
    {
        return ruleBuilder.Must(element =>
        {
            if (element.ValueKind != JsonValueKind.Object) return false;
            if (!element.TryGetProperty("type", out var typeProp)) return false;
            return typeProp.GetString() == "doc";
        }).WithMessage("'{PropertyName}' must be a valid ProseMirror document (type: 'doc').");
    }

    public static IRuleBuilderOptions<T, JsonElement?> MustBeValidOptionalJsonBlock<T>(this IRuleBuilder<T, JsonElement?> ruleBuilder)
    {
        return ruleBuilder.Must(element =>
        {
            if (!element.HasValue) return true;
            if (element.Value.ValueKind != JsonValueKind.Object) return false;
            if (!element.Value.TryGetProperty("type", out var typeProp)) return false;
            return typeProp.GetString() == "doc";
        }).WithMessage("'{PropertyName}' must be a valid ProseMirror document (type: 'doc').");
    }
}

public class UpsertCourseOverviewValidator : Validator<UpsertCourseOverviewRequest>
{
    public UpsertCourseOverviewValidator()
    {
        RuleFor(x => x.DescriptionContent)
            .MustBeValidJsonBlock();

        RuleFor(x => x.TargetAudienceContent)
            .MustBeValidOptionalJsonBlock();

        RuleFor(x => x.LearningObjectivesContent)
            .MustBeValidOptionalJsonBlock();
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
            req.DescriptionContent.GetRawText(), 
            req.TargetAudienceContent?.GetRawText(), 
            req.LearningObjectivesContent?.GetRawText());
            
        var result = await _module.Send(command, ct);
        
        if (result.IsError)
        {
            await this.SendErrorResponseAsync(result.Errors, ct);
            return;
        }

        await Send.OkAsync(ct);
    }
}

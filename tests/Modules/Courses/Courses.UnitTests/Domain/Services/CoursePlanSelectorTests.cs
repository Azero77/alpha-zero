using System.Text.Json;
using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using AlphaZero.Modules.Courses.Domain.Services;
using AlphaZero.Shared.Domain;
using FluentAssertions;
using Xunit;

namespace AlphaZero.Modules.Courses.UnitTests.Domain.Services;

public class CoursePlanSelectorTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CourseId = Guid.NewGuid();

    private readonly CoursePlanSelector _selector = new();

    private static Course CreateCourseWithPlans(params (string name, int? window)[] plans)
    {
        var course = Course.Create(CourseId, TenantId, "Physics 101", "Desc", Guid.NewGuid()).Value;
        foreach (var (name, window) in plans)
        {
            var result = course.AddPlan(name, Guid.NewGuid(), window);
            result.IsError.Should().BeFalse();
        }
        return course;
    }

    [Fact]
    public void SelectPlan_Should_SelectPlanFromMetadata_WhenPresent()
    {
        var course = CreateCourseWithPlans(("Basic", null), ("Premium", 30));
        var premium = course.Plans.Single(p => p.Name == "Premium");
        using var metadata = JsonDocument.Parse($"{{\"plan\":\"{premium.Id}\"}}");

        var result = _selector.SelectPlan(course, metadata, ResourceArn.ForCourse(TenantId, CourseId));

        result.IsError.Should().BeFalse();
        result.Value.Id.Should().Be(premium.Id);
    }

    [Fact]
    public void SelectPlan_Should_SelectPlanFromLegacyArn_WhenNoMetadata()
    {
        var course = CreateCourseWithPlans(("Basic", null), ("Premium", 30));
        var premium = course.Plans.Single(p => p.Name == "Premium");
        var arn = ResourceArn.Create($"az:course:{TenantId}:course/{CourseId}/plan/{premium.Id}").Value;

        var result = _selector.SelectPlan(course, null, arn);

        result.IsError.Should().BeFalse();
        result.Value.Id.Should().Be(premium.Id);
    }

    [Fact]
    public void SelectPlan_Should_PreferMetadata_OverLegacyArn()
    {
        var course = CreateCourseWithPlans(("Basic", null), ("Premium", 30));
        var premium = course.Plans.Single(p => p.Name == "Premium");
        var basic = course.Plans.Single(p => p.Name == "Basic");
        var arn = ResourceArn.Create($"az:course:{TenantId}:course/{CourseId}/plan/{basic.Id}").Value;
        using var metadata = JsonDocument.Parse($"{{\"plan\":\"{premium.Id}\"}}");

        var result = _selector.SelectPlan(course, metadata, arn);

        result.IsError.Should().BeFalse();
        result.Value.Id.Should().Be(premium.Id);
    }

    [Fact]
    public void SelectPlan_Should_SelectSinglePlan_WhenNoReference()
    {
        var course = CreateCourseWithPlans(("Standard", null));

        var result = _selector.SelectPlan(course, null, ResourceArn.ForCourse(TenantId, CourseId));

        result.IsError.Should().BeFalse();
        result.Value.Id.Should().Be(course.Plans.Single().Id);
    }

    [Fact]
    public void SelectPlan_Should_FailAmbiguous_WhenMultiplePlansAndNoReference()
    {
        var course = CreateCourseWithPlans(("Basic", null), ("Premium", 30));

        var result = _selector.SelectPlan(course, null, ResourceArn.ForCourse(TenantId, CourseId));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("CoursePlan.Ambiguous");
        result.FirstError.Description.Should().Contain("does not specify one");
    }

    [Fact]
    public void SelectPlan_Should_FailNotFound_WhenMetadataPlanUnknown()
    {
        var course = CreateCourseWithPlans(("Standard", null));
        var unknown = Guid.NewGuid();
        using var metadata = JsonDocument.Parse($"{{\"plan\":\"{unknown}\"}}");

        var result = _selector.SelectPlan(course, metadata, ResourceArn.ForCourse(TenantId, CourseId));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("CoursePlan.NotFound");
        result.FirstError.Description.Should().Contain(unknown.ToString());
    }

    [Fact]
    public void SelectPlan_Should_FailNotFound_WhenMetadataMalformed()
    {
        var course = CreateCourseWithPlans(("Standard", null));
        using var metadata = JsonDocument.Parse("{\"plan\":\"not-a-guid\"}");

        var result = _selector.SelectPlan(course, metadata, ResourceArn.ForCourse(TenantId, CourseId));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("CoursePlan.NotFound");
    }

    [Fact]
    public void SelectPlan_Should_FailNotFound_WhenCourseHasNoPlans()
    {
        var course = CreateCourseWithPlans();

        var result = _selector.SelectPlan(course, null, ResourceArn.ForCourse(TenantId, CourseId));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("CoursePlan.NotFound");
    }
}

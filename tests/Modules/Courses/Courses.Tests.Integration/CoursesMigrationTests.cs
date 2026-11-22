using AlphaZero.Modules.Courses.Domain.Aggregates.Courses;
using Courses.Tests.Integration.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Courses.Tests.Integration;

public sealed class CoursesMigrationTests(ApiFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Course_plan_catalog_migration_is_discovered_and_applied()
    {
        await using var connection = new NpgsqlConnection(Factory.DbConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT count(*)
            FROM information_schema.columns
            WHERE table_schema = 'Courses'
              AND table_name = 'CoursePlans'
              AND column_name IN ('CatalogSourceVersion', 'IsRemoved');
            """, connection);

        ((long)(await command.ExecuteScalarAsync())!).Should().Be(2);

        DbContext.Database.GetMigrations().Should()
            .Contain("20261122120000_AddCoursePlanCatalogVersion");
    }

    [Fact]
    public async Task Removed_plan_can_be_replaced_with_the_same_name()
    {
        var course = Course.Create(Guid.NewGuid(), Guid.NewGuid(), "Math 101", null, Guid.NewGuid()).Value;
        course.AddSection("Section");
        var firstPlan = course.AddPlan("Standard", Guid.NewGuid()).Value;
        DbContext.Courses.Add(course);
        await DbContext.SaveChangesAsync();

        course.RemovePlan(firstPlan.Id).IsError.Should().BeFalse();
        await DbContext.SaveChangesAsync();

        course.AddPlan("Standard", Guid.NewGuid()).IsError.Should().BeFalse();
        await DbContext.SaveChangesAsync();

        (await DbContext.Set<Course>().IgnoreQueryFilters().CountAsync(c => c.Id == course.Id)).Should().Be(1);
    }
}

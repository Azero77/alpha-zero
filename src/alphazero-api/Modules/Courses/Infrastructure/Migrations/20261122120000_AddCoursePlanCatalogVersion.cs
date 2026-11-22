using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.Courses.Infrastructure.Migrations;

public partial class AddCoursePlanCatalogVersion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "CatalogSourceVersion",
            schema: "Courses",
            table: "CoursePlans",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<bool>(
            name: "IsRemoved",
            schema: "Courses",
            table: "CoursePlans",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "CatalogSourceVersion", schema: "Courses", table: "CoursePlans");
        migrationBuilder.DropColumn(name: "IsRemoved", schema: "Courses", table: "CoursePlans");
    }
}

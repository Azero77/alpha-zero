using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.Courses.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class softDeleteTimeAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "OnDeleted",
                schema: "Courses",
                table: "CourseSectionItem",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OnDeleted",
                schema: "Courses",
                table: "CourseSection",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OnDeleted",
                schema: "Courses",
                table: "CourseSectionItem");

            migrationBuilder.DropColumn(
                name: "OnDeleted",
                schema: "Courses",
                table: "CourseSection");
        }
    }
}

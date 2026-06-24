using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.Courses.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseAnalytics_MovedToInfra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CourseAnalytics",
                schema: "Courses",
                columns: table => new
                {
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalEnrollments = table.Column<int>(type: "integer", nullable: false),
                    SumOfCompletionPercentages = table.Column<double>(type: "double precision", nullable: false),
                    ItemCompletions = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseAnalytics", x => x.CourseId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CourseAnalytics_CourseId",
                schema: "Courses",
                table: "CourseAnalytics",
                column: "CourseId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CourseAnalytics",
                schema: "Courses");
        }
    }
}

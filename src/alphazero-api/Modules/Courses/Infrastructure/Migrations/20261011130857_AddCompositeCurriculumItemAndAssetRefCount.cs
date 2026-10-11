using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.Courses.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompositeCurriculumItemAndAssetRefCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<JsonElement>(
                name: "Content",
                schema: "Courses",
                table: "CurriculumItems",
                type: "jsonb",
                nullable: false,
                defaultValue: System.Text.Json.JsonDocument.Parse("[]", new System.Text.Json.JsonDocumentOptions()).RootElement);

            migrationBuilder.AddColumn<int>(
                name: "RefCount",
                schema: "Courses",
                table: "CourseAssets",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Content",
                schema: "Courses",
                table: "CurriculumItems");

            migrationBuilder.DropColumn(
                name: "RefCount",
                schema: "Courses",
                table: "CourseAssets");
        }
    }
}

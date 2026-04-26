using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.Courses.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<JsonElement>(
                name: "Metadata",
                schema: "Courses",
                table: "CourseSectionItem",
                type: "jsonb",
                nullable: false,
                defaultValue: System.Text.Json.JsonDocument.Parse("{}", new System.Text.Json.JsonDocumentOptions()).RootElement);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Metadata",
                schema: "Courses",
                table: "CourseSectionItem");
        }
    }
}

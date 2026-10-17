using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.Courses.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorCurriculumResourceTphAndRemoveContentJsonb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CurriculumResources_CourseAssets_CourseAssetId",
                schema: "Courses",
                table: "CurriculumResources");

            migrationBuilder.DropColumn(
                name: "Metadata",
                schema: "Courses",
                table: "CurriculumResources");

            migrationBuilder.DropColumn(
                name: "Content",
                schema: "Courses",
                table: "CurriculumItems");

            migrationBuilder.AlterColumn<Guid>(
                name: "CourseAssetId",
                schema: "Courses",
                table: "CurriculumResources",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "AssessmentId",
                schema: "Courses",
                table: "CurriculumResources",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DocumentId",
                schema: "Courses",
                table: "CurriculumResources",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentType",
                schema: "Courses",
                table: "CurriculumResources",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DurationSeconds",
                schema: "Courses",
                table: "CurriculumResources",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                schema: "Courses",
                table: "CurriculumResources",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PassingScore",
                schema: "Courses",
                table: "CurriculumResources",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResourceType",
                schema: "Courses",
                table: "CurriculumResources",
                type: "character varying(21)",
                maxLength: 21,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TextContent",
                schema: "Courses",
                table: "CurriculumResources",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                schema: "Courses",
                table: "CurriculumResources",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VariantName",
                schema: "Courses",
                table: "CurriculumResources",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VideoId",
                schema: "Courses",
                table: "CurriculumResources",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CurriculumResources_CourseAssets_CourseAssetId",
                schema: "Courses",
                table: "CurriculumResources",
                column: "CourseAssetId",
                principalSchema: "Courses",
                principalTable: "CourseAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CurriculumResources_CourseAssets_CourseAssetId",
                schema: "Courses",
                table: "CurriculumResources");

            migrationBuilder.DropColumn(
                name: "AssessmentId",
                schema: "Courses",
                table: "CurriculumResources");

            migrationBuilder.DropColumn(
                name: "DocumentId",
                schema: "Courses",
                table: "CurriculumResources");

            migrationBuilder.DropColumn(
                name: "DocumentType",
                schema: "Courses",
                table: "CurriculumResources");

            migrationBuilder.DropColumn(
                name: "DurationSeconds",
                schema: "Courses",
                table: "CurriculumResources");

            migrationBuilder.DropColumn(
                name: "IsPublic",
                schema: "Courses",
                table: "CurriculumResources");

            migrationBuilder.DropColumn(
                name: "PassingScore",
                schema: "Courses",
                table: "CurriculumResources");

            migrationBuilder.DropColumn(
                name: "ResourceType",
                schema: "Courses",
                table: "CurriculumResources");

            migrationBuilder.DropColumn(
                name: "TextContent",
                schema: "Courses",
                table: "CurriculumResources");

            migrationBuilder.DropColumn(
                name: "Title",
                schema: "Courses",
                table: "CurriculumResources");

            migrationBuilder.DropColumn(
                name: "VariantName",
                schema: "Courses",
                table: "CurriculumResources");

            migrationBuilder.DropColumn(
                name: "VideoId",
                schema: "Courses",
                table: "CurriculumResources");

            migrationBuilder.AlterColumn<Guid>(
                name: "CourseAssetId",
                schema: "Courses",
                table: "CurriculumResources",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<JsonElement>(
                name: "Metadata",
                schema: "Courses",
                table: "CurriculumResources",
                type: "jsonb",
                nullable: false,
                defaultValue: System.Text.Json.JsonDocument.Parse("", new System.Text.Json.JsonDocumentOptions()).RootElement);

            migrationBuilder.AddColumn<JsonElement>(
                name: "Content",
                schema: "Courses",
                table: "CurriculumItems",
                type: "jsonb",
                nullable: false,
                defaultValue: System.Text.Json.JsonDocument.Parse("", new System.Text.Json.JsonDocumentOptions()).RootElement);

            migrationBuilder.AddForeignKey(
                name: "FK_CurriculumResources_CourseAssets_CourseAssetId",
                schema: "Courses",
                table: "CurriculumResources",
                column: "CourseAssetId",
                principalSchema: "Courses",
                principalTable: "CourseAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}

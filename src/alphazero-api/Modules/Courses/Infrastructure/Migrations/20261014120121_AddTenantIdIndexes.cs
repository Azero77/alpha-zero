using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.Courses.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantIdIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CourseAssets_Courses_CourseId",
                schema: "Courses",
                table: "CourseAssets");

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
                name: "TextContent",
                schema: "Courses",
                table: "CurriculumResources");

            migrationBuilder.DropColumn(
                name: "VideoId",
                schema: "Courses",
                table: "CurriculumResources");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Courses",
                table: "CurriculumItems");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Courses",
                table: "CourseSections");

            migrationBuilder.DropColumn(
                name: "RefCount",
                schema: "Courses",
                table: "CourseAssets");

            migrationBuilder.AddColumn<JsonElement>(
                name: "Content",
                schema: "Courses",
                table: "CurriculumResources",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_TenantId",
                schema: "Courses",
                table: "Subjects",
                column: "TenantId",
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollements_TenantId",
                schema: "Courses",
                table: "Enrollements",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Courses_TenantId",
                schema: "Courses",
                table: "Courses",
                column: "TenantId",
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_CourseOverviews_TenantId",
                schema: "Courses",
                table: "CourseOverviews",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseAssets_TenantId",
                schema: "Courses",
                table: "CourseAssets",
                column: "TenantId");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CurriculumResources_CourseAssets_CourseAssetId",
                schema: "Courses",
                table: "CurriculumResources");

            migrationBuilder.DropIndex(
                name: "IX_Subjects_TenantId",
                schema: "Courses",
                table: "Subjects");

            migrationBuilder.DropIndex(
                name: "IX_Enrollements_TenantId",
                schema: "Courses",
                table: "Enrollements");

            migrationBuilder.DropIndex(
                name: "IX_Courses_TenantId",
                schema: "Courses",
                table: "Courses");

            migrationBuilder.DropIndex(
                name: "IX_CourseOverviews_TenantId",
                schema: "Courses",
                table: "CourseOverviews");

            migrationBuilder.DropIndex(
                name: "IX_CourseAssets_TenantId",
                schema: "Courses",
                table: "CourseAssets");

            migrationBuilder.DropColumn(
                name: "Content",
                schema: "Courses",
                table: "CurriculumResources");

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
                name: "TextContent",
                schema: "Courses",
                table: "CurriculumResources",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VideoId",
                schema: "Courses",
                table: "CurriculumResources",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Courses",
                table: "CurriculumItems",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "Courses",
                table: "CourseSections",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "RefCount",
                schema: "Courses",
                table: "CourseAssets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddForeignKey(
                name: "FK_CourseAssets_Courses_CourseId",
                schema: "Courses",
                table: "CourseAssets",
                column: "CourseId",
                principalSchema: "Courses",
                principalTable: "Courses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

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
    }
}

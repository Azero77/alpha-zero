using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.Courses.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SoftDeletetToCoursesAndSubjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Subjects_TenantId",
                schema: "Courses",
                table: "Subjects");

            migrationBuilder.DropIndex(
                name: "IX_Courses_TenantId",
                schema: "Courses",
                table: "Courses");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "Courses",
                table: "Subjects",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "OnDeleted",
                schema: "Courses",
                table: "Subjects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "Courses",
                table: "Courses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "OnDeleted",
                schema: "Courses",
                table: "Courses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_IsDeleted",
                schema: "Courses",
                table: "Subjects",
                column: "IsDeleted",
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_TenantId",
                schema: "Courses",
                table: "Subjects",
                column: "TenantId",
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_Courses_IsDeleted",
                schema: "Courses",
                table: "Courses",
                column: "IsDeleted",
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_Courses_TenantId",
                schema: "Courses",
                table: "Courses",
                column: "TenantId",
                filter: "\"IsDeleted\" = FALSE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Subjects_IsDeleted",
                schema: "Courses",
                table: "Subjects");

            migrationBuilder.DropIndex(
                name: "IX_Subjects_TenantId",
                schema: "Courses",
                table: "Subjects");

            migrationBuilder.DropIndex(
                name: "IX_Courses_IsDeleted",
                schema: "Courses",
                table: "Courses");

            migrationBuilder.DropIndex(
                name: "IX_Courses_TenantId",
                schema: "Courses",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "Courses",
                table: "Subjects");

            migrationBuilder.DropColumn(
                name: "OnDeleted",
                schema: "Courses",
                table: "Subjects");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "Courses",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "OnDeleted",
                schema: "Courses",
                table: "Courses");

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_TenantId",
                schema: "Courses",
                table: "Subjects",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Courses_TenantId",
                schema: "Courses",
                table: "Courses",
                column: "TenantId");
        }
    }
}

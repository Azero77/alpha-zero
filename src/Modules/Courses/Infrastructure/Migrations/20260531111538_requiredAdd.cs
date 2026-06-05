using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.Courses.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class requiredAdd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CourseSection_Courses_CourseId",
                schema: "Courses",
                table: "CourseSection");

            migrationBuilder.DropForeignKey(
                name: "FK_CourseSectionItem_CourseSection_SectionId",
                schema: "Courses",
                table: "CourseSectionItem");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CourseSection",
                schema: "Courses",
                table: "CourseSection");

            migrationBuilder.RenameTable(
                name: "CourseSection",
                schema: "Courses",
                newName: "CourseSections",
                newSchema: "Courses");

            migrationBuilder.RenameIndex(
                name: "IX_CourseSection_TenantId",
                schema: "Courses",
                table: "CourseSections",
                newName: "IX_CourseSections_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_CourseSection_IsDeleted",
                schema: "Courses",
                table: "CourseSections",
                newName: "IX_CourseSections_IsDeleted");

            migrationBuilder.RenameIndex(
                name: "IX_CourseSection_CourseId",
                schema: "Courses",
                table: "CourseSections",
                newName: "IX_CourseSections_CourseId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CourseSections",
                schema: "Courses",
                table: "CourseSections",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CourseSectionItem_CourseSections_SectionId",
                schema: "Courses",
                table: "CourseSectionItem",
                column: "SectionId",
                principalSchema: "Courses",
                principalTable: "CourseSections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CourseSections_Courses_CourseId",
                schema: "Courses",
                table: "CourseSections",
                column: "CourseId",
                principalSchema: "Courses",
                principalTable: "Courses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CourseSectionItem_CourseSections_SectionId",
                schema: "Courses",
                table: "CourseSectionItem");

            migrationBuilder.DropForeignKey(
                name: "FK_CourseSections_Courses_CourseId",
                schema: "Courses",
                table: "CourseSections");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CourseSections",
                schema: "Courses",
                table: "CourseSections");

            migrationBuilder.RenameTable(
                name: "CourseSections",
                schema: "Courses",
                newName: "CourseSection",
                newSchema: "Courses");

            migrationBuilder.RenameIndex(
                name: "IX_CourseSections_TenantId",
                schema: "Courses",
                table: "CourseSection",
                newName: "IX_CourseSection_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_CourseSections_IsDeleted",
                schema: "Courses",
                table: "CourseSection",
                newName: "IX_CourseSection_IsDeleted");

            migrationBuilder.RenameIndex(
                name: "IX_CourseSections_CourseId",
                schema: "Courses",
                table: "CourseSection",
                newName: "IX_CourseSection_CourseId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CourseSection",
                schema: "Courses",
                table: "CourseSection",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CourseSection_Courses_CourseId",
                schema: "Courses",
                table: "CourseSection",
                column: "CourseId",
                principalSchema: "Courses",
                principalTable: "Courses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CourseSectionItem_CourseSection_SectionId",
                schema: "Courses",
                table: "CourseSectionItem",
                column: "SectionId",
                principalSchema: "Courses",
                principalTable: "CourseSection",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

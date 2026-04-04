using System;
using System.Collections;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.Courses.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Courses");

            migrationBuilder.CreateTable(
                name: "Courses",
                schema: "Courses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    NextAvailableBitIndex = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Courses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Enrollements",
                schema: "Courses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Progress = table.Column<BitArray>(type: "varbit", nullable: false),
                    EnrolledOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Enrollements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Subjects",
                schema: "Courses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CourseSection",
                schema: "Courses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseSection", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseSection_Courses_CourseId",
                        column: x => x.CourseId,
                        principalSchema: "Courses",
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CourseSectionItem",
                schema: "Courses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    BitIndex = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ItemType = table.Column<string>(type: "character varying(21)", maxLength: 21, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseSectionItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseSectionItem_CourseSection_SectionId",
                        column: x => x.SectionId,
                        principalSchema: "Courses",
                        principalTable: "CourseSection",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Courses_TenantId",
                schema: "Courses",
                table: "Courses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseSection_CourseId",
                schema: "Courses",
                table: "CourseSection",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseSection_IsDeleted",
                schema: "Courses",
                table: "CourseSection",
                column: "IsDeleted",
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_CourseSection_TenantId",
                schema: "Courses",
                table: "CourseSection",
                column: "TenantId",
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_CourseSectionItem_IsDeleted",
                schema: "Courses",
                table: "CourseSectionItem",
                column: "IsDeleted",
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_CourseSectionItem_SectionId",
                schema: "Courses",
                table: "CourseSectionItem",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseSectionItem_TenantId",
                schema: "Courses",
                table: "CourseSectionItem",
                column: "TenantId",
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollements_StudentId_CourseId",
                schema: "Courses",
                table: "Enrollements",
                columns: new[] { "StudentId", "CourseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Enrollements_TenantId",
                schema: "Courses",
                table: "Enrollements",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_TenantId",
                schema: "Courses",
                table: "Subjects",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CourseSectionItem",
                schema: "Courses");

            migrationBuilder.DropTable(
                name: "Enrollements",
                schema: "Courses");

            migrationBuilder.DropTable(
                name: "Subjects",
                schema: "Courses");

            migrationBuilder.DropTable(
                name: "CourseSection",
                schema: "Courses");

            migrationBuilder.DropTable(
                name: "Courses",
                schema: "Courses");
        }
    }
}

using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.Courses.Infrastructure.Migrations
{
    public partial class RefactorCourseSectionItems : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Drop foreign key on legacy table
            migrationBuilder.DropForeignKey(
                name: "FK_CourseSectionItem_CourseSections_SectionId",
                schema: "Courses",
                table: "CourseSectionItem");

            // 2. Drop legacy table
            migrationBuilder.DropTable(
                name: "CourseSectionItem",
                schema: "Courses");

            // 3. Create CurriculumItems table
            migrationBuilder.CreateTable(
                name: "CurriculumItems",
                schema: "Courses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    BitIndex = table.Column<int>(type: "integer", nullable: false),
                    MainType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    OnDeleted = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurriculumItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CurriculumItems_CourseSections_SectionId",
                        column: x => x.SectionId,
                        principalSchema: "Courses",
                        principalTable: "CourseSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // 4. Create CurriculumResources table
            migrationBuilder.CreateTable(
                name: "CurriculumResources",
                schema: "Courses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CurriculumItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Arn = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Metadata = table.Column<JsonElement>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurriculumResources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CurriculumResources_CurriculumItems_CurriculumItemId",
                        column: x => x.CurriculumItemId,
                        principalSchema: "Courses",
                        principalTable: "CurriculumItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // 5. Create indices on CurriculumItems
            migrationBuilder.CreateIndex(
                name: "IX_CurriculumItems_IsDeleted",
                schema: "Courses",
                table: "CurriculumItems",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumItems_SectionId",
                schema: "Courses",
                table: "CurriculumItems",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumItems_TenantId",
                schema: "Courses",
                table: "CurriculumItems",
                column: "TenantId");

            // 6. Create index on CurriculumResources
            migrationBuilder.CreateIndex(
                name: "IX_CurriculumResources_CurriculumItemId",
                schema: "Courses",
                table: "CurriculumResources",
                column: "CurriculumItemId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop new tables
            migrationBuilder.DropTable(
                name: "CurriculumResources",
                schema: "Courses");

            migrationBuilder.DropTable(
                name: "CurriculumItems",
                schema: "Courses");

            // Recreate legacy table
            migrationBuilder.CreateTable(
                name: "CourseSectionItem",
                schema: "Courses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    BitIndex = table.Column<int>(type: "integer", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Metadata = table.Column<JsonElement>(type: "jsonb", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    OnDeleted = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ItemType = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseSectionItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseSectionItem_CourseSections_SectionId",
                        column: x => x.SectionId,
                        principalSchema: "Courses",
                        principalTable: "CourseSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Recreate indices on legacy table
            migrationBuilder.CreateIndex(
                name: "IX_CourseSectionItem_IsDeleted",
                schema: "Courses",
                table: "CourseSectionItem",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_CourseSectionItem_SectionId",
                schema: "Courses",
                table: "CourseSectionItem",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseSectionItem_TenantId",
                schema: "Courses",
                table: "CourseSectionItem",
                column: "TenantId");
        }
    }
}

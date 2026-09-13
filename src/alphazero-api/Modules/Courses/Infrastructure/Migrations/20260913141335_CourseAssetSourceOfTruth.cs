using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.Courses.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CourseAssetSourceOfTruth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Arn",
                schema: "Courses",
                table: "CurriculumResources");

            migrationBuilder.DropColumn(
                name: "Type",
                schema: "Courses",
                table: "CurriculumResources");

            migrationBuilder.AddColumn<int>(
                name: "ProgressActiveItems",
                schema: "Courses",
                table: "Enrollements",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "CourseAssetId",
                schema: "Courses",
                table: "CurriculumResources",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "CourseAssets",
                schema: "Courses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceArn = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UploadedUtcAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AssetType = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    QuestionsNumber = table.Column<int>(type: "integer", nullable: true),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    FileName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Size = table.Column<long>(type: "bigint", nullable: true),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Duration = table.Column<TimeSpan>(type: "interval", nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RelativeStreamingUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseAssets_Courses_CourseId",
                        column: x => x.CourseId,
                        principalSchema: "Courses",
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumResources_CourseAssetId",
                schema: "Courses",
                table: "CurriculumResources",
                column: "CourseAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseAssets_CourseId_State",
                schema: "Courses",
                table: "CourseAssets",
                columns: new[] { "CourseId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_CourseAssets_ResourceArn",
                schema: "Courses",
                table: "CourseAssets",
                column: "ResourceArn",
                unique: true);

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

            migrationBuilder.DropTable(
                name: "CourseAssets",
                schema: "Courses");

            migrationBuilder.DropIndex(
                name: "IX_CurriculumResources_CourseAssetId",
                schema: "Courses",
                table: "CurriculumResources");

            migrationBuilder.DropColumn(
                name: "ProgressActiveItems",
                schema: "Courses",
                table: "Enrollements");

            migrationBuilder.DropColumn(
                name: "CourseAssetId",
                schema: "Courses",
                table: "CurriculumResources");

            migrationBuilder.AddColumn<string>(
                name: "Arn",
                schema: "Courses",
                table: "CurriculumResources",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                schema: "Courses",
                table: "CurriculumResources",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }
    }
}

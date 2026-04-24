using System;
using AlphaZero.Modules.Assessments.Domain.Models.Content;
using AlphaZero.Modules.Assessments.Domain.Models.Submissions;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.Assessments.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Assessments");

            migrationBuilder.CreateTable(
                name: "Assessments",
                schema: "Assessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Type = table.Column<string>(type: "text", nullable: false),
                    PassingScore = table.Column<decimal>(type: "numeric", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CurrentVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    OnDeleted = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assessments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Submissions",
                schema: "Assessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    TotalScore = table.Column<decimal>(type: "numeric", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GradedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Responses = table.Column<AssessmentSubmissionResponses>(type: "jsonb", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Submissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AssessmentVersion",
                schema: "Assessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<AssessmentContent>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentVersion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssessmentVersion_Assessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalSchema: "Assessments",
                        principalTable: "Assessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_IsDeleted",
                schema: "Assessments",
                table: "Assessments",
                column: "IsDeleted",
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_TenantId",
                schema: "Assessments",
                table: "Assessments",
                column: "TenantId",
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentVersion_AssessmentId",
                schema: "Assessments",
                table: "AssessmentVersion",
                column: "AssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentVersion_TenantId",
                schema: "Assessments",
                table: "AssessmentVersion",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_StudentId_AssessmentId",
                schema: "Assessments",
                table: "Submissions",
                columns: new[] { "StudentId", "AssessmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_TenantId",
                schema: "Assessments",
                table: "Submissions",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssessmentVersion",
                schema: "Assessments");

            migrationBuilder.DropTable(
                name: "Submissions",
                schema: "Assessments");

            migrationBuilder.DropTable(
                name: "Assessments",
                schema: "Assessments");
        }
    }
}

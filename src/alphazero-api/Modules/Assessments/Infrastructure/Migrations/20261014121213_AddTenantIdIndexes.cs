using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.Assessments.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantIdIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Submissions_TenantId",
                schema: "Assessments",
                table: "Submissions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentVersion_TenantId",
                schema: "Assessments",
                table: "AssessmentVersion",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_TenantId",
                schema: "Assessments",
                table: "Assessments",
                column: "TenantId",
                filter: "\"IsDeleted\" = FALSE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Submissions_TenantId",
                schema: "Assessments",
                table: "Submissions");

            migrationBuilder.DropIndex(
                name: "IX_AssessmentVersion_TenantId",
                schema: "Assessments",
                table: "AssessmentVersion");

            migrationBuilder.DropIndex(
                name: "IX_Assessments_TenantId",
                schema: "Assessments",
                table: "Assessments");
        }
    }
}

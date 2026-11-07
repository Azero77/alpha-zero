using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.Courses.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FailProvisioningRecordsUniqueTenantCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FailedProvisioningRecords_TenantId_AccessCodeId",
                schema: "Courses",
                table: "FailedProvisioningRecords");

            migrationBuilder.CreateIndex(
                name: "IX_FailedProvisioningRecords_TenantId_AccessCodeId",
                schema: "Courses",
                table: "FailedProvisioningRecords",
                columns: new[] { "TenantId", "AccessCodeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FailedProvisioningRecords_TenantId_AccessCodeId",
                schema: "Courses",
                table: "FailedProvisioningRecords");

            migrationBuilder.CreateIndex(
                name: "IX_FailedProvisioningRecords_TenantId_AccessCodeId",
                schema: "Courses",
                table: "FailedProvisioningRecords",
                columns: new[] { "TenantId", "AccessCodeId" });
        }
    }
}

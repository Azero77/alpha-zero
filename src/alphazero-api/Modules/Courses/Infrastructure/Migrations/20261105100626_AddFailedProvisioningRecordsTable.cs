using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.Courses.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFailedProvisioningRecordsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FailedProvisioningRecords",
                schema: "Courses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccessCodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetResourceArn = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastAttemptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FailedProvisioningRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FailedProvisioningRecords_TenantId",
                schema: "Courses",
                table: "FailedProvisioningRecords",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FailedProvisioningRecords_TenantId_AccessCodeId",
                schema: "Courses",
                table: "FailedProvisioningRecords",
                columns: new[] { "TenantId", "AccessCodeId" });

            migrationBuilder.CreateIndex(
                name: "IX_FailedProvisioningRecords_TenantId_Status",
                schema: "Courses",
                table: "FailedProvisioningRecords",
                columns: new[] { "TenantId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FailedProvisioningRecords",
                schema: "Courses");
        }
    }
}

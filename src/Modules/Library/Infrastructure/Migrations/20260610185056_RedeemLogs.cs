using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.Library.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RedeemLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RedemptionAuditLogs",
                schema: "Library",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: true),
                    AccessCodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    RedeemedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StrategyId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TargetResourceArn = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    RedeemedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DeviceFingerprint = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RedemptionAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RedemptionAuditLogs_AccessCodeId",
                schema: "Library",
                table: "RedemptionAuditLogs",
                column: "AccessCodeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RedemptionAuditLogs_TenantId_LibraryId_RedeemedAt",
                schema: "Library",
                table: "RedemptionAuditLogs",
                columns: new[] { "TenantId", "LibraryId", "RedeemedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RedemptionAuditLogs",
                schema: "Library");
        }
    }
}

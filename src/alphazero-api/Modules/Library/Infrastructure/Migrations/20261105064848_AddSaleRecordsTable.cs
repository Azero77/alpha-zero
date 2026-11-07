using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.Library.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSaleRecordsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SaleRecords",
                schema: "Library",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccessCodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SoldByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    SoldAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RedemptionAuditLogs_TenantId",
                schema: "Library",
                table: "RedemptionAuditLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleRecords_AccessCodeId",
                schema: "Library",
                table: "SaleRecords",
                column: "AccessCodeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaleRecords_TenantId",
                schema: "Library",
                table: "SaleRecords",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleRecords_TenantId_LibraryId_SoldAt",
                schema: "Library",
                table: "SaleRecords",
                columns: new[] { "TenantId", "LibraryId", "SoldAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SaleRecords",
                schema: "Library");

            migrationBuilder.DropIndex(
                name: "IX_RedemptionAuditLogs_TenantId",
                schema: "Library",
                table: "RedemptionAuditLogs");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.Library.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLibraryAnalyticsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LibraryAnalytics",
                schema: "Library",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    MintedCount = table.Column<int>(type: "integer", nullable: false),
                    DistributedCount = table.Column<int>(type: "integer", nullable: false),
                    SoldCount = table.Column<int>(type: "integer", nullable: false),
                    RedeemedCount = table.Column<int>(type: "integer", nullable: false),
                    VoidedCount = table.Column<int>(type: "integer", nullable: false),
                    ExpiredCount = table.Column<int>(type: "integer", nullable: false),
                    OutstandingCount = table.Column<int>(type: "integer", nullable: false),
                    VoidedAfterSaleCount = table.Column<int>(type: "integer", nullable: false),
                    TotalRevenue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RevenueByPeriod = table.Column<string>(type: "jsonb", nullable: false),
                    SalesByPeriod = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LibraryAnalytics", x => new { x.TenantId, x.LibraryId, x.BatchId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_LibraryAnalytics_TenantId",
                schema: "Library",
                table: "LibraryAnalytics",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LibraryAnalytics",
                schema: "Library");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.Library.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLibraryAuditLogActionIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_LibraryAuditLogs_TenantId_Action_OccurredAt",
                schema: "Library",
                table: "LibraryAuditLogs",
                columns: new[] { "TenantId", "Action", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LibraryAuditLogs_TenantId_Action_OccurredAt",
                schema: "Library",
                table: "LibraryAuditLogs");
        }
    }
}

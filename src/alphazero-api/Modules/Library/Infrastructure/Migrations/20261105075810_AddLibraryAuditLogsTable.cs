using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.Library.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLibraryAuditLogsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LibraryAuditLogs",
                schema: "Library",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AggregateId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Payload = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LibraryAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LibraryAuditLogs_AggregateId",
                schema: "Library",
                table: "LibraryAuditLogs",
                column: "AggregateId");

            migrationBuilder.CreateIndex(
                name: "IX_LibraryAuditLogs_EventId",
                schema: "Library",
                table: "LibraryAuditLogs",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LibraryAuditLogs_TenantId",
                schema: "Library",
                table: "LibraryAuditLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LibraryAuditLogs_TenantId_LibraryId_OccurredAt",
                schema: "Library",
                table: "LibraryAuditLogs",
                columns: new[] { "TenantId", "LibraryId", "OccurredAt" });

            // Enforce immutability: deny UPDATE and DELETE at the database engine level via trigger and table grants
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION ""Library"".deny_library_audit_log_mutation()
RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION 'Updates and deletes are not permitted on LibraryAuditLogs table.';
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_deny_library_audit_log_mutation
BEFORE UPDATE OR DELETE ON ""Library"".""LibraryAuditLogs""
FOR EACH ROW EXECUTE FUNCTION ""Library"".deny_library_audit_log_mutation();

REVOKE UPDATE, DELETE ON ""Library"".""LibraryAuditLogs"" FROM PUBLIC;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TRIGGER IF EXISTS trg_deny_library_audit_log_mutation ON ""Library"".""LibraryAuditLogs"";
DROP FUNCTION IF EXISTS ""Library"".deny_library_audit_log_mutation();
");

            migrationBuilder.DropTable(
                name: "LibraryAuditLogs",
                schema: "Library");
        }
    }
}

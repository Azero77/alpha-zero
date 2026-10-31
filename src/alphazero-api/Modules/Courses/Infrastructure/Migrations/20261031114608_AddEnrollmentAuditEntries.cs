using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.Courses.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEnrollmentAuditEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EnrollmentAuditEntries",
                schema: "Courses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventKind = table.Column<string>(type: "text", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Metadata = table.Column<string>(type: "jsonb", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnrollmentAuditEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EnrollmentAuditEntries_SubjectId_OccurredOn",
                schema: "Courses",
                table: "EnrollmentAuditEntries",
                columns: new[] { "SubjectId", "OccurredOn" });

            migrationBuilder.CreateIndex(
                name: "IX_EnrollmentAuditEntries_TenantId",
                schema: "Courses",
                table: "EnrollmentAuditEntries",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EnrollmentAuditEntries",
                schema: "Courses");
        }
    }
}

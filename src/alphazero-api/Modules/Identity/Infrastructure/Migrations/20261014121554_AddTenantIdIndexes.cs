using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantIdIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InlinePolicy",
                schema: "Identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InlinePolicy", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantUsers_TenantId",
                schema: "Identity",
                table: "TenantUsers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantPrincipalAssignments_TenantId",
                schema: "Identity",
                table: "TenantPrincipalAssignments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_InlinePolicy_TenantId",
                schema: "Identity",
                table: "InlinePolicy",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InlinePolicy",
                schema: "Identity");

            migrationBuilder.DropIndex(
                name: "IX_TenantUsers_TenantId",
                schema: "Identity",
                table: "TenantUsers");

            migrationBuilder.DropIndex(
                name: "IX_TenantPrincipalAssignments_TenantId",
                schema: "Identity",
                table: "TenantPrincipalAssignments");
        }
    }
}

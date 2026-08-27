using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AuthenticationSchemas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Identity");
            migrationBuilder.RenameTable(
                name: "UserDevices",
                newName: "UserDevices",
                newSchema: "Identity");

            migrationBuilder.RenameTable(
                name: "TenantUsers",
                newName: "TenantUsers",
                newSchema: "Identity");

            migrationBuilder.RenameTable(
                name: "TenantPrincipalAssignments",
                newName: "TenantPrincipalAssignments",
                newSchema: "Identity");

            migrationBuilder.RenameTable(
                name: "Principals",
                newName: "Principals",
                newSchema: "Identity");

            migrationBuilder.RenameTable(
                name: "PrincipalManagedPolicyAssignments",
                newName: "PrincipalManagedPolicyAssignments",
                newSchema: "Identity");

            migrationBuilder.RenameTable(
                name: "ManagedPolicies",
                newName: "ManagedPolicies",
                newSchema: "Identity");

            migrationBuilder.RenameTable(
                name: "ConditionDefinitions",
                newName: "ConditionDefinitions",
                newSchema: "Identity");

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

            migrationBuilder.RenameTable(
                name: "UserDevices",
                schema: "Identity",
                newName: "UserDevices");

            migrationBuilder.RenameTable(
                name: "TenantUsers",
                schema: "Identity",
                newName: "TenantUsers");

            migrationBuilder.RenameTable(
                name: "TenantPrincipalAssignments",
                schema: "Identity",
                newName: "TenantPrincipalAssignments");

            migrationBuilder.RenameTable(
                name: "Principals",
                schema: "Identity",
                newName: "Principals");

            migrationBuilder.RenameTable(
                name: "PrincipalManagedPolicyAssignments",
                schema: "Identity",
                newName: "PrincipalManagedPolicyAssignments");

            migrationBuilder.RenameTable(
                name: "ManagedPolicies",
                schema: "Identity",
                newName: "ManagedPolicies");

            migrationBuilder.RenameTable(
                name: "ConditionDefinitions",
                schema: "Identity",
                newName: "ConditionDefinitions");
        }
    }
}

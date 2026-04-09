using System;
using System.Collections.Generic;
using AlphaZero.Modules.Identity.Domain.Models;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ManagedPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Statements = table.Column<List<PolicyTemplateStatement>>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagedPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Principals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IdentityId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    PrincipalType = table.Column<string>(type: "text", nullable: false),
                    PrincipalScopeUrn = table.Column<string>(type: "text", nullable: false),
                    ScopeResourceType = table.Column<string>(type: "text", nullable: true),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Principals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Policies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Statements = table.Column<IReadOnlyCollection<PolicyStatement>>(type: "jsonb", nullable: false),
                    PrincipalId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Policies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Policies_Principals_PrincipalId",
                        column: x => x.PrincipalId,
                        principalTable: "Principals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PrincipalPolicyAssignments",
                columns: table => new
                {
                    PrincipalId = table.Column<Guid>(type: "uuid", nullable: false),
                    ManagedPolicyId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrincipalPolicyAssignments", x => new { x.PrincipalId, x.ManagedPolicyId });
                    table.ForeignKey(
                        name: "FK_PrincipalPolicyAssignments_ManagedPolicies_ManagedPolicyId",
                        column: x => x.ManagedPolicyId,
                        principalTable: "ManagedPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PrincipalPolicyAssignments_Principals_PrincipalId",
                        column: x => x.PrincipalId,
                        principalTable: "Principals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Policies_PrincipalId",
                table: "Policies",
                column: "PrincipalId");

            migrationBuilder.CreateIndex(
                name: "IX_PrincipalPolicyAssignments_ManagedPolicyId",
                table: "PrincipalPolicyAssignments",
                column: "ManagedPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_Principals_IdentityId",
                table: "Principals",
                column: "IdentityId");

            migrationBuilder.CreateIndex(
                name: "IX_Principals_ResourceId_ScopeResourceType",
                table: "Principals",
                columns: new[] { "ResourceId", "ScopeResourceType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Policies");

            migrationBuilder.DropTable(
                name: "PrincipalPolicyAssignments");

            migrationBuilder.DropTable(
                name: "ManagedPolicies");

            migrationBuilder.DropTable(
                name: "Principals");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
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
                    Statements = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagedPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrincipalTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    PrincipalType = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrincipalTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdentityId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ActiveSessionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrincipalManagedPolicyAssignments",
                columns: table => new
                {
                    PrincipalId = table.Column<Guid>(type: "uuid", nullable: false),
                    ManagedPolicyId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrincipalManagedPolicyAssignments", x => new { x.PrincipalId, x.ManagedPolicyId });
                    table.ForeignKey(
                        name: "FK_PrincipalManagedPolicyAssignments_ManagedPolicies_ManagedPo~",
                        column: x => x.ManagedPolicyId,
                        principalTable: "ManagedPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PrincipalManagedPolicyAssignments_PrincipalTemplates_Princi~",
                        column: x => x.PrincipalId,
                        principalTable: "PrincipalTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Principals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PrincipalScopePattern = table.Column<string>(type: "text", nullable: true),
                    ScopeResourceType = table.Column<string>(type: "text", nullable: true),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    InlinePolicies = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Principals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Principals_PrincipalTemplates_Id",
                        column: x => x.Id,
                        principalTable: "PrincipalTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantPrinciaplAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrincipalTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceArn = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantPrinciaplAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantPrinciaplAssignments_PrincipalTemplates_PrincipalTemp~",
                        column: x => x.PrincipalTemplateId,
                        principalTable: "PrincipalTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenantPrinciaplAssignments_TenantUsers_TenantUserId",
                        column: x => x.TenantUserId,
                        principalTable: "TenantUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrincipalManagedPolicyAssignments_ManagedPolicyId",
                table: "PrincipalManagedPolicyAssignments",
                column: "ManagedPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_Principals_TenantUserId",
                table: "Principals",
                column: "TenantUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantPrinciaplAssignments_PrincipalTemplateId",
                table: "TenantPrinciaplAssignments",
                column: "PrincipalTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantPrinciaplAssignments_TenantId",
                table: "TenantPrinciaplAssignments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantPrinciaplAssignments_TenantUserId_PrincipalTemplateId~",
                table: "TenantPrinciaplAssignments",
                columns: new[] { "TenantUserId", "PrincipalTemplateId", "ResourceArn" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantUsers_IdentityId_TenantId",
                table: "TenantUsers",
                columns: new[] { "IdentityId", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantUsers_TenantId",
                table: "TenantUsers",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrincipalManagedPolicyAssignments");

            migrationBuilder.DropTable(
                name: "Principals");

            migrationBuilder.DropTable(
                name: "TenantPrinciaplAssignments");

            migrationBuilder.DropTable(
                name: "ManagedPolicies");

            migrationBuilder.DropTable(
                name: "PrincipalTemplates");

            migrationBuilder.DropTable(
                name: "TenantUsers");
        }
    }
}

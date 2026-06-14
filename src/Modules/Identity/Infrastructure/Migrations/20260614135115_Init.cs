using System;
using AlphaZero.Modules.Identity.Domain.Models.Principals;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConditionDefinitions",
                columns: table => new
                {
                    Name = table.Column<string>(type: "text", nullable: false),
                    InnerCondition = table.Column<IConditionNode>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConditionDefinitions", x => x.Name);
                });

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
                name: "Principals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PrincipalType = table.Column<string>(type: "text", nullable: false),
                    PrincipalScopePattern = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    InlinePolicies = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Principals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdentityId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    MainDeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastMainDeviceSwitchDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                        name: "FK_PrincipalManagedPolicyAssignments_Principals_PrincipalId",
                        column: x => x.PrincipalId,
                        principalTable: "Principals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantPrincipalAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrincipalId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceArn = table.Column<string>(type: "text", nullable: false),
                    TimeCreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantPrincipalAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantPrincipalAssignments_Principals_PrincipalId",
                        column: x => x.PrincipalId,
                        principalTable: "Principals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenantPrincipalAssignments_TenantUsers_TenantUserId",
                        column: x => x.TenantUserId,
                        principalTable: "TenantUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Platform = table.Column<string>(type: "text", nullable: false),
                    PublicKey = table.Column<string>(type: "text", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserDevices_TenantUsers_TenantUserId",
                        column: x => x.TenantUserId,
                        principalTable: "TenantUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ManagedPolicies_Name",
                table: "ManagedPolicies",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrincipalManagedPolicyAssignments_ManagedPolicyId",
                table: "PrincipalManagedPolicyAssignments",
                column: "ManagedPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_Principals_Username_TenantId",
                table: "Principals",
                columns: new[] { "Username", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantPrincipalAssignments_PrincipalId",
                table: "TenantPrincipalAssignments",
                column: "PrincipalId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantPrincipalAssignments_TenantId",
                table: "TenantPrincipalAssignments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantPrincipalAssignments_TenantUserId_PrincipalId_Resourc~",
                table: "TenantPrincipalAssignments",
                columns: new[] { "TenantUserId", "PrincipalId", "ResourceArn" },
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

            migrationBuilder.CreateIndex(
                name: "IX_UserDevices_TenantUserId",
                table: "UserDevices",
                column: "TenantUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConditionDefinitions");

            migrationBuilder.DropTable(
                name: "PrincipalManagedPolicyAssignments");

            migrationBuilder.DropTable(
                name: "TenantPrincipalAssignments");

            migrationBuilder.DropTable(
                name: "UserDevices");

            migrationBuilder.DropTable(
                name: "ManagedPolicies");

            migrationBuilder.DropTable(
                name: "Principals");

            migrationBuilder.DropTable(
                name: "TenantUsers");
        }
    }
}

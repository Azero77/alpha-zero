using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.Library.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Library");

            migrationBuilder.CreateTable(
                name: "AccessCodes",
                schema: "Library",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: true),
                    StrategyId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TargetResourceArn = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Metadata = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RedeemedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RedeemedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Libraries",
                schema: "Library",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Address = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ContactNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Libraries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LibraryAllowedResources",
                schema: "Library",
                columns: table => new
                {
                    ResourcePatternValue = table.Column<string>(type: "text", nullable: false),
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LibraryAllowedResources", x => new { x.LibraryId, x.ResourcePatternValue });
                    table.ForeignKey(
                        name: "FK_LibraryAllowedResources_Libraries_LibraryId",
                        column: x => x.LibraryId,
                        principalSchema: "Library",
                        principalTable: "Libraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessCodes_CodeHash",
                schema: "Library",
                table: "AccessCodes",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccessCodes_LibraryId",
                schema: "Library",
                table: "AccessCodes",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessCodes_TenantId",
                schema: "Library",
                table: "AccessCodes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Libraries_TenantId",
                schema: "Library",
                table: "Libraries",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessCodes",
                schema: "Library");

            migrationBuilder.DropTable(
                name: "LibraryAllowedResources",
                schema: "Library");

            migrationBuilder.DropTable(
                name: "Libraries",
                schema: "Library");
        }
    }
}

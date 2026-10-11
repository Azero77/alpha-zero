using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.Documents.Infrastructure.Persistance.Migrations
{
    /// <inheritdoc />
    public partial class AddIsPublicToDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FileHash",
                schema: "documents",
                table: "Documents",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                schema: "documents",
                table: "Documents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Dictionary<string, object>>(
                name: "Metadata",
                schema: "documents",
                table: "Documents",
                type: "jsonb",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "Scope",
                schema: "documents",
                table: "Documents",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "documents",
                table: "Documents",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                schema: "documents",
                table: "Documents",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "DocumentProcessingSagaState",
                schema: "documents",
                columns: table => new
                {
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentState = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    UploadTimeoutTokenId = table.Column<Guid>(type: "uuid", nullable: true),
                    FaultedDeletionTokenId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentProcessingSagaState", x => x.CorrelationId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_FileHash",
                schema: "documents",
                table: "Documents",
                column: "FileHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentProcessingSagaState",
                schema: "documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_FileHash",
                schema: "documents",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "FileHash",
                schema: "documents",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "IsPublic",
                schema: "documents",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "Metadata",
                schema: "documents",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "Scope",
                schema: "documents",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "documents",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "Type",
                schema: "documents",
                table: "Documents");
        }
    }
}

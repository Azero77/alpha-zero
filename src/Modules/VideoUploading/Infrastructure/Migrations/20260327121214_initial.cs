using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "video_uploading");

            migrationBuilder.CreateTable(
                name: "VideoState",
                schema: "video_uploading",
                columns: table => new
                {
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentState = table.Column<string>(type: "text", nullable: false),
                    MediaConverterJobId = table.Column<string>(type: "text", nullable: true),
                    Key = table.Column<string>(type: "text", nullable: true),
                    ProcessingStarted = table.Column<bool>(type: "boolean", nullable: false),
                    IsFailed = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoState", x => x.CorrelationId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VideoState_CorrelationId",
                schema: "video_uploading",
                table: "VideoState",
                column: "CorrelationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VideoState",
                schema: "video_uploading");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SodtDeleteforVideo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "video_uploading",
                table: "Videos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Videos_IsDeleted",
                schema: "video_uploading",
                table: "Videos",
                column: "IsDeleted",
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_Videos_TenantId",
                schema: "video_uploading",
                table: "Videos",
                column: "TenantId",
                filter: "\"IsDeleted\" = FALSE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Videos_IsDeleted",
                schema: "video_uploading",
                table: "Videos");

            migrationBuilder.DropIndex(
                name: "IX_Videos_TenantId",
                schema: "video_uploading",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "video_uploading",
                table: "Videos");
        }
    }
}

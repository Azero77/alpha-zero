using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemovedSourceKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceKey",
                schema: "video_uploading",
                table: "Videos");

            migrationBuilder.AddColumn<int>(
                name: "Thumbnail_State",
                schema: "video_uploading",
                table: "Videos",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Thumbnail_State",
                schema: "video_uploading",
                table: "Videos");

            migrationBuilder.AddColumn<string>(
                name: "SourceKey",
                schema: "video_uploading",
                table: "Videos",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "");
        }
    }
}

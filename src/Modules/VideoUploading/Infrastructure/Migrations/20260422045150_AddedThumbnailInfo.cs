using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedThumbnailInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomThumbnailKey",
                schema: "video_uploading",
                table: "VideoState",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomThumbnailKey",
                schema: "video_uploading",
                table: "Videos",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailUrl",
                schema: "video_uploading",
                table: "Videos",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UseCustomThumbnail",
                schema: "video_uploading",
                table: "Videos",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomThumbnailKey",
                schema: "video_uploading",
                table: "VideoState");

            migrationBuilder.DropColumn(
                name: "CustomThumbnailKey",
                schema: "video_uploading",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "ThumbnailUrl",
                schema: "video_uploading",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "UseCustomThumbnail",
                schema: "video_uploading",
                table: "Videos");
        }
    }
}

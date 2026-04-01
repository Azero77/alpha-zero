using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemovedTemp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BucketName",
                schema: "video_uploading",
                table: "VideoState");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BucketName",
                schema: "video_uploading",
                table: "VideoState",
                type: "text",
                nullable: true);
        }
    }
}

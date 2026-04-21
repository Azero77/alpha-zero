using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedEncryptionInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EncryptionMethod",
                schema: "video_uploading",
                table: "VideoState",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Metadata_EncryptionMethod",
                schema: "video_uploading",
                table: "Videos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Metadata_TranscodingMethod",
                schema: "video_uploading",
                table: "Videos",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EncryptionMethod",
                schema: "video_uploading",
                table: "VideoState");

            migrationBuilder.DropColumn(
                name: "Metadata_EncryptionMethod",
                schema: "video_uploading",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "Metadata_TranscodingMethod",
                schema: "video_uploading",
                table: "Videos");
        }
    }
}

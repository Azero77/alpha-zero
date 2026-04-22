using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddkeysToMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Metadata_OriginalFileName",
                schema: "video_uploading",
                table: "Videos",
                newName: "OriginalFileName");

            migrationBuilder.RenameColumn(
                name: "Metadata_FileSize",
                schema: "video_uploading",
                table: "Videos",
                newName: "FileSize");

            migrationBuilder.RenameColumn(
                name: "Metadata_ContentType",
                schema: "video_uploading",
                table: "Videos",
                newName: "ContentType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "OriginalFileName",
                schema: "video_uploading",
                table: "Videos",
                newName: "Metadata_OriginalFileName");

            migrationBuilder.RenameColumn(
                name: "FileSize",
                schema: "video_uploading",
                table: "Videos",
                newName: "Metadata_FileSize");

            migrationBuilder.RenameColumn(
                name: "ContentType",
                schema: "video_uploading",
                table: "Videos",
                newName: "Metadata_ContentType");
        }
    }
}

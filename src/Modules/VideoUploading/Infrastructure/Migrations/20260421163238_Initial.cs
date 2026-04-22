using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "video_uploading");

            migrationBuilder.CreateTable(
                name: "Videos",
                schema: "video_uploading",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Metadata_OriginalFileName = table.Column<string>(type: "text", nullable: false),
                    Metadata_ContentType = table.Column<string>(type: "text", nullable: false),
                    Metadata_FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Metadata_TranscodingMethod = table.Column<string>(type: "text", nullable: false),
                    Metadata_EncryptionMethod = table.Column<string>(type: "text", nullable: true),
                    Duration = table.Column<TimeSpan>(type: "interval", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    SourceKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    OutputFolder = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    OnDeleted = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Videos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VideoSecrets",
                schema: "video_uploading",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VideoId = table.Column<Guid>(type: "uuid", nullable: false),
                    KeyId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    KeyValue = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    IV = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoSecrets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VideoState",
                schema: "video_uploading",
                columns: table => new
                {
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentState = table.Column<string>(type: "text", nullable: false),
                    MediaConverterJobId = table.Column<string>(type: "text", nullable: true),
                    Key = table.Column<string>(type: "text", nullable: true),
                    SourceWidth = table.Column<int>(type: "integer", nullable: true),
                    SourceHeight = table.Column<int>(type: "integer", nullable: true),
                    Duration = table.Column<TimeSpan>(type: "interval", nullable: true),
                    S3OutputPrefix = table.Column<string>(type: "text", nullable: true),
                    FinalUrl = table.Column<string>(type: "text", nullable: true),
                    EncryptionMethod = table.Column<string>(type: "text", nullable: true),
                    IsFailed = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoState", x => x.CorrelationId);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_VideoSecrets_VideoId",
                schema: "video_uploading",
                table: "VideoSecrets",
                column: "VideoId",
                unique: true);

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
                name: "Videos",
                schema: "video_uploading");

            migrationBuilder.DropTable(
                name: "VideoSecrets",
                schema: "video_uploading");

            migrationBuilder.DropTable(
                name: "VideoState",
                schema: "video_uploading");
        }
    }
}

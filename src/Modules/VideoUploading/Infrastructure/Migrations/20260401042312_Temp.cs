using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Temp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProcessingStarted",
                schema: "video_uploading",
                table: "VideoState");

            migrationBuilder.AddColumn<string>(
                name: "BucketName",
                schema: "video_uploading",
                table: "VideoState",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "Duration",
                schema: "video_uploading",
                table: "VideoState",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinalUrl",
                schema: "video_uploading",
                table: "VideoState",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "S3OutputPrefix",
                schema: "video_uploading",
                table: "VideoState",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceHeight",
                schema: "video_uploading",
                table: "VideoState",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceWidth",
                schema: "video_uploading",
                table: "VideoState",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BucketName",
                schema: "video_uploading",
                table: "VideoState");

            migrationBuilder.DropColumn(
                name: "Duration",
                schema: "video_uploading",
                table: "VideoState");

            migrationBuilder.DropColumn(
                name: "FinalUrl",
                schema: "video_uploading",
                table: "VideoState");

            migrationBuilder.DropColumn(
                name: "S3OutputPrefix",
                schema: "video_uploading",
                table: "VideoState");

            migrationBuilder.DropColumn(
                name: "SourceHeight",
                schema: "video_uploading",
                table: "VideoState");

            migrationBuilder.DropColumn(
                name: "SourceWidth",
                schema: "video_uploading",
                table: "VideoState");

            migrationBuilder.AddColumn<bool>(
                name: "ProcessingStarted",
                schema: "video_uploading",
                table: "VideoState",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}

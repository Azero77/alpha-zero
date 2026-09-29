using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.VideoUploading.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemovedPlaybackUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentState",
                schema: "video_uploading",
                table: "VideoState");

            migrationBuilder.DropColumn(
                name: "CustomThumbnailKey",
                schema: "video_uploading",
                table: "VideoState");

            migrationBuilder.DropColumn(
                name: "Duration",
                schema: "video_uploading",
                table: "VideoState");

            migrationBuilder.DropColumn(
                name: "EncryptionMethod",
                schema: "video_uploading",
                table: "VideoState");

            migrationBuilder.DropColumn(
                name: "FinalUrl",
                schema: "video_uploading",
                table: "VideoState");

            migrationBuilder.DropColumn(
                name: "IsFailed",
                schema: "video_uploading",
                table: "VideoState");

            migrationBuilder.DropColumn(
                name: "Key",
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

            migrationBuilder.DropColumn(
                name: "TargetResourceArn",
                schema: "video_uploading",
                table: "VideoState");

            migrationBuilder.DropColumn(
                name: "CustomThumbnailKey",
                schema: "video_uploading",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "OutputFolder",
                schema: "video_uploading",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "ThumbnailUrl",
                schema: "video_uploading",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "Thumbnail_State",
                schema: "video_uploading",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "UseCustomThumbnail",
                schema: "video_uploading",
                table: "Videos");

            migrationBuilder.RenameColumn(
                name: "CorrelationId",
                schema: "video_uploading",
                table: "VideoState",
                newName: "VideoId");

            migrationBuilder.RenameIndex(
                name: "IX_VideoState_CorrelationId",
                schema: "video_uploading",
                table: "VideoState",
                newName: "IX_VideoState_VideoId");

            migrationBuilder.AddColumn<int>(
                name: "Stage",
                schema: "video_uploading",
                table: "VideoState",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsDefaultThumbnail",
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
                name: "Stage",
                schema: "video_uploading",
                table: "VideoState");

            migrationBuilder.DropColumn(
                name: "IsDefaultThumbnail",
                schema: "video_uploading",
                table: "Videos");

            migrationBuilder.RenameColumn(
                name: "VideoId",
                schema: "video_uploading",
                table: "VideoState",
                newName: "CorrelationId");

            migrationBuilder.RenameIndex(
                name: "IX_VideoState_VideoId",
                schema: "video_uploading",
                table: "VideoState",
                newName: "IX_VideoState_CorrelationId");

            migrationBuilder.AddColumn<string>(
                name: "CurrentState",
                schema: "video_uploading",
                table: "VideoState",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CustomThumbnailKey",
                schema: "video_uploading",
                table: "VideoState",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "Duration",
                schema: "video_uploading",
                table: "VideoState",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EncryptionMethod",
                schema: "video_uploading",
                table: "VideoState",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinalUrl",
                schema: "video_uploading",
                table: "VideoState",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFailed",
                schema: "video_uploading",
                table: "VideoState",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Key",
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

            migrationBuilder.AddColumn<string>(
                name: "TargetResourceArn",
                schema: "video_uploading",
                table: "VideoState",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomThumbnailKey",
                schema: "video_uploading",
                table: "Videos",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutputFolder",
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

            migrationBuilder.AddColumn<int>(
                name: "Thumbnail_State",
                schema: "video_uploading",
                table: "Videos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "UseCustomThumbnail",
                schema: "video_uploading",
                table: "Videos",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}

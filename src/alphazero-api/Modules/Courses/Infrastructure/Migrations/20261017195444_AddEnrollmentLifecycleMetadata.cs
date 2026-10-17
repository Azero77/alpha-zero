using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.Courses.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEnrollmentLifecycleMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LastStatusChangedBy",
                schema: "Courses",
                table: "Enrollements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastStatusChangedOn",
                schema: "Courses",
                table: "Enrollements",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastStatusChangedBy",
                schema: "Courses",
                table: "Enrollements");

            migrationBuilder.DropColumn(
                name: "LastStatusChangedOn",
                schema: "Courses",
                table: "Enrollements");
        }
    }
}

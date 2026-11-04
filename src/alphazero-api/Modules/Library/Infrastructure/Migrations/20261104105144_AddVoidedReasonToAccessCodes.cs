using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.Library.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVoidedReasonToAccessCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VoidedReason",
                schema: "Library",
                table: "AccessCodes",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VoidedReason",
                schema: "Library",
                table: "AccessCodes");
        }
    }
}

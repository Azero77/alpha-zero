using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.Tenants.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantBranding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PrimaryColor",
                schema: "Tenants",
                table: "Tenants",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "#2563eb",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DarkModeLogoUrl",
                schema: "Tenants",
                table: "Tenants",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FaviconUrl",
                schema: "Tenants",
                table: "Tenants",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DarkModeLogoUrl",
                schema: "Tenants",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "FaviconUrl",
                schema: "Tenants",
                table: "Tenants");

            migrationBuilder.AlterColumn<string>(
                name: "PrimaryColor",
                schema: "Tenants",
                table: "Tenants",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldDefaultValue: "#2563eb");
        }
    }
}

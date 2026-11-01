using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.Courses.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEnrollmentExpiryIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Enrollements_Status_AccessExpiresOn",
                schema: "Courses",
                table: "Enrollements",
                columns: new[] { "Status", "AccessExpiresOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Enrollements_Status_AccessExpiresOn",
                schema: "Courses",
                table: "Enrollements");
        }
    }
}

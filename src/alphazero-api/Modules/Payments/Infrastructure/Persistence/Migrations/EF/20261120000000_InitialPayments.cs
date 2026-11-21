using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.Payments.Infrastructure.Persistence.Migrations.EF;

/// <summary>
/// EF model baseline for the SQL-created Payments schema.
/// The SQL migration with this same ID creates the schema and records this migration as applied.
/// </summary>
public partial class InitialPayments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // The matching SQL baseline created this schema before EF migrations were introduced.
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Payments schema rollback is intentionally not supported.
    }
}

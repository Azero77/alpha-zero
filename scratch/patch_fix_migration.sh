#!/bin/bash
# Revert AddMassTransitOutbox to empty
cat << 'INNER_EOF' > src/alphazero-api/Modules/Courses/Infrastructure/Migrations/20260929123157_AddMassTransitOutbox.cs
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaZero.Modules.Courses.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMassTransitOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
INNER_EOF

# Copy Assessments to FixMassTransitOutbox
cat src/alphazero-api/Modules/Assessments/Infrastructure/Migrations/20260929114148_AddMassTransitOutbox.cs | sed 's/schema: "Assessments"/schema: "Courses"/g' > src/alphazero-api/Modules/Courses/Infrastructure/Migrations/20260930125758_FixMassTransitOutbox.cs
sed -i 's/namespace AlphaZero.Modules.Assessments.Infrastructure.Migrations/namespace AlphaZero.Modules.Courses.Infrastructure.Migrations/g' src/alphazero-api/Modules/Courses/Infrastructure/Migrations/20260930125758_FixMassTransitOutbox.cs
sed -i 's/public partial class AddMassTransitOutbox/public partial class FixMassTransitOutbox/g' src/alphazero-api/Modules/Courses/Infrastructure/Migrations/20260930125758_FixMassTransitOutbox.cs

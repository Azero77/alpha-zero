#!/bin/bash
cat src/alphazero-api/Modules/Assessments/Infrastructure/Migrations/20260929114148_AddMassTransitOutbox.cs | sed 's/schema: "Assessments"/schema: "Courses"/g' > src/alphazero-api/Modules/Courses/Infrastructure/Migrations/20260929123157_AddMassTransitOutbox.cs
sed -i 's/namespace AlphaZero.Modules.Assessments.Infrastructure.Migrations/namespace AlphaZero.Modules.Courses.Infrastructure.Migrations/g' src/alphazero-api/Modules/Courses/Infrastructure/Migrations/20260929123157_AddMassTransitOutbox.cs

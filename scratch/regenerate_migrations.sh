#!/bin/bash
set -e

# Delete all Migrations folders
find src/alphazero-api/Modules -type d -name "Migrations" -exec rm -rf {} +
rm -rf src/alphazero-api/AlphaZero.Shared/Migrations

# Recreate for modules
MODULES=("Documents" "Tenants" "Courses" "Library" "VideoUploading" "Assessments" "Identity")

for m in "${MODULES[@]}"; do
    echo "Creating Initial migration for $m..."
    make migrations-create NAME=Initial m=$m
done

# Recreate for Shared (JobServiceSagaDbContext)
echo "Creating Initial migration for Shared..."
dotnet ef migrations add Initial --project src/alphazero-api/AlphaZero.Shared/AlphaZero.Shared.csproj --startup-project src/alphazero-api/AlphaZero.API/AlphaZero.API.csproj --context MassTransit.EntityFrameworkCoreIntegration.JobServiceSagaDbContext --output-dir Migrations

echo "Done!"

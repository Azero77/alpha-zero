# ------------------------
# Variables
# ------------------------
m ?= VideoUploading
PROJECT = src/alphazero-api/Modules/$(m)/Infrastructure/Infrastructure.csproj
API = src/alphazero-api/AlphaZero.API/AlphaZero.API.csproj
o = Migrations
DbContext = AlphaZero.Modules.$(m).Infrastructure.Persistance.AppDbContext

# ------------------------
# Help
# ------------------------
migrations-help:
	@echo "Makefile commands:"
	@echo "  make migrations-create NAME=MigrationName [-m=ModuleName]  # Create an EF migration for an existing module"
	@echo "  make migrations-migrate [-m=ModuleName]                   # Apply module migrations"
	@echo "  make migrations-payments                                  # Apply SQL Payments migration(s)"
	@echo "  make migrations-clean [-m=ModuleName]                     # Remove last EF migration"

# ------------------------
# Create migration
# ------------------------
migrations-create:
ifndef NAME
	$(error "NAME variable is required. Usage: make migrations-create NAME=MigrationName [-m=ModuleName]")
endif
	dotnet ef migrations add $(NAME) --project $(PROJECT) --startup-project $(API) --output-dir $(o) --context $(DbContext)

# ------------------------
# Apply migrations
# ------------------------
migrations-migrate:
	dotnet ef database update --project $(PROJECT) --startup-project $(API)

# ------------------------
# Remove last migration
# ------------------------
migrations-clean:
	dotnet ef migrations remove --project $(PROJECT) --startup-project $(API) --force --context $(DbContext)

# Payments is SQL-first; this is the same tracked runner used by Payments module startup.
migrations-payments:
	dotnet run --project $(API) -- --apply-payments-migrations

# Deployment invocation: set PAYMENTS_MIGRATION_CONNECTION_STRING to a role-creating migration principal.
deploy-migrations-payments:
	@test -n "$$PAYMENTS_MIGRATION_CONNECTION_STRING" || (echo 'PAYMENTS_MIGRATION_CONNECTION_STRING must be set' >&2; exit 1)
	PAYMENTS__MIGRATIONCONNECTIONSTRING="$$PAYMENTS_MIGRATION_CONNECTION_STRING" dotnet run --project $(API) -c Release -- --apply-payments-migrations


#-------------------------
# Run Aspire
#-------------------------

run:
	dotnet run --project src/alphazero-api/aspire/AlphaZero.AppHost/AlphaZero.AppHost.csproj

#-------------------------
# Deploy CDK Infrastructure
#-------------------------
cdk-deploy:
	cdk deploy --all --app "dotnet run -p infrastructure/AlphaZero.Cdk/AlphaZero.Cdk.csproj"
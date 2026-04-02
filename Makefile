# ------------------------
# Variables
# ------------------------
m ?= VideoUploading
PROJECT = src/Modules/$(m)/Infrastructure/Infrastructure.csproj
API = src/Alphazero.API/Alphazero.API.csproj
o = Migrations
DbContext = AppDbContext

# ------------------------
# Help
# ------------------------
migrations-help:
	@echo "Makefile commands:"
	@echo "  make migrations-create NAME=MigrationName [-m=ModuleName]  # Create a new migration"
	@echo "  make migrations-migrate [-m=ModuleName]                   # Apply pending migrations"
	@echo "  make migrations-clean [-m=ModuleName]                     # Remove last migration"

# ------------------------
# Create migration
# ------------------------
migrations-create:
ifndef NAME
	$(error "NAME variable is required. Usage: make migrations-create NAME=MigrationName [-m=ModuleName]")
endif
	dotnet ef migrations add $(NAME) --project $(PROJECT) --startup-project $(API) --output-dir $(o)

# ------------------------
# Apply migrations
# ------------------------
migrations-migrate:
	dotnet ef database update --project $(PROJECT) --startup-project $(API)

# ------------------------
# Remove last migration
# ------------------------
migrations-clean:
	dotnet ef migrations remove --project $(PROJECT) --startup-project $(API) --force


#-------------------------
# Run Aspire
#-------------------------

run:
	dotnet run --project src/aspire/AlphaZero.AppHost/AlphaZero.AppHost.csproj
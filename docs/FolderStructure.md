# 📂 Repository Folder Structure: AlphaZero

This document defines the organization of the AlphaZero GitHub repository. It follows **Enterprise Clean Architecture** principles within a **Modular Monolith** pattern.

---

## 🏗️ High-Level Overview

```text
AlphaZero/
├── 📂 docs/                  # Project documentation (PRD, ERD, ADRs)
├── 📂 src/                   # Main source code
│   ├── 📂 BuildingBlocks/    # Shared kernel logic (Cross-cutting concerns)
│   ├── 📂 Modules/           # Independent business modules
│   └── 📂 Host/              # Bootstrapper / Entry Point project
├── 📂 tests/                 # Automated test suite (Mirrors src/)
├── 📂 infrastructure/        # DevOps & Deployment files
├── 📂 scripts/               # DB migrations, seed data, automation
├── .gitignore
├── AlphaZero.sln             # Global Solution File
└── README.md
```

---

## 🧩 Detailed Source Structure (`src/`)

### 1. BuildingBlocks (The Shared Kernel)
Common logic shared across all modules. No module-specific business logic belongs here.
*   **AlphaZero.BuildingBlocks.Domain:** Base `Entity`, `IAggregateRoot`, `IDomainEvent`.
*   **AlphaZero.BuildingBlocks.Application:** Common `Result` types, MediatR behaviors (Logging, Validation).
*   **AlphaZero.BuildingBlocks.Infrastructure:** Tenant Resolution (extracting `TenantId`), JWT processing, Event Bus interfaces.

### 2. Modules (Business Logic)
Each module is a "Mini-Service" inside the monolith.
*   **📂 Identity:** Auth, Roles (11 RBAC), User management.
*   **📂 Tenants:** Institutional setup, Subdomains, Branding/Themes.
*   **📂 Courses:** Lesson production, Video hosting, **Bitmask Progress Tracking**.
*   **📂 Assessments:** Quiz engine, Math/LaTeX support, **Handwritten grading workflows**.
*   **📂 Library:** Access code generation, Inventory, Offline redemption.
*   **📂 Community:** Discussion threads, Verified answers, Notifications.

**Internal Module Layers (Example: Courses):**
```text
📂 src/Modules/Courses/
├── 📂 AlphaZero.Modules.Courses.Domain/         # Business logic, Rules, Bitmask ValueObjects
├── 📂 AlphaZero.Modules.Courses.Application/    # Use Cases (Commands/Queries), Event Handlers
├── 📂 AlphaZero.Modules.Courses.Infrastructure/ # EF Core, S3 Integration, Repository Impls
└── 📂 AlphaZero.Modules.Courses.Api/            # Controllers / Minimal API Endpoints
```

### 3. Host (The Bootstrapper)
*   **AlphaZero.Host.Api:** The actual ASP.NET Core project. It registers all modules and configures the HTTP pipeline (Middleware, Swagger, etc.).

---

## 🧪 Testing Structure (`tests/`)

Tests are strictly separated by type to ensure high performance and reliability.
*   **📂 UnitTests:** Fast tests for Domain logic (e.g., Bitmask arithmetic).
*   **📂 IntegrationTests:** Testing Module + Infrastructure (e.g., Database persistence).
*   **📂 FunctionalTests:** API-level tests (e.g., "Full Code Redemption Flow").
*   **📂 ArchTests:** Ensuring modular boundaries aren't crossed (e.g., "Courses cannot reference Identity.Infrastructure").

---

## 🛠️ Infrastructure & Scripts

*   **📂 infrastructure/deploy:** Dockerfiles, Docker-compose, Terraform/CloudFormation for AWS.
*   **📂 infrastructure/monitoring:** Logging (Serilog) and OpenTelemetry configurations.
*   **📂 scripts:**
    *   `/db-migrations`: SQL scripts for initial setup.
    *   `/seed-data`: Default Subjects and Grade mappings for tenants.
    *   `/local-dev`: Scripts to spin up local PostgreSQL/DynamoDB containers.

---

## 📏 Architecture Rules
1.  **Strict Isolation:** A module can only communicate with another module via an **Internal Event Bus** or a strictly defined **Public Interface**. Direct DB joins between modules are forbidden.
2.  **Tenant Awareness:** Every request entering the `Host.Api` must pass through the Tenant Middleware to populate the `ITenantProvider`.
3.  **Clean Dependency:** Domain must have **zero** dependencies on Infrastructure.

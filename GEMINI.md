# 🪐 AlphaZero Learning Academy - Context & Mandates

## 🎯 Project Identity
**AlphaZero** is a high-performance, multi-tenant SaaS e-learning platform. It provides white-labeled "Online Academies" optimized for low-bandwidth environments (Syria/MENA) using a unique physical library code economy.

## 🏗️ Architectural Mandates
- **Pattern:** Modular Monolith following **Enterprise Clean Architecture**.
- **Isolation:** Modules (Identity, Courses, Tenants, etc.) must remain strictly isolated. 
  - No direct database joins between modules.
  - Cross-module communication via **Internal MediatR (Request/Response)** or **MassTransit Mediator (In-Memory Pub/Sub)**.
- **Tenant Awareness:** Every request is tenant-scoped. Use the `ITenantProvider` and ensure `TenantId` is handled in all queries and commands.

## 🧠 Core Technical Constraints
- **Progress Tracking:** Completion logic MUST use **Bitmasking** (`VARBIT`) to minimize DB footprint and payload size.
- **Video Streaming:** Serverless pipeline (Upload -> S3 -> SQS -> MediaConvert -> HLS/Dash).
- **Security:** One active session per student. Logging in on a new device MUST terminate previous sessions.
- **Offline Economy:** Physical "Library Codes" are the primary payment/unlock mechanism.

## 🛠️ Implementation Standards
1. **Research First:** Always refer to `docs/PRD.md` and `docs/UserStories.md` for feature truth.
2. **Domain Integrity:** The Domain layer must have zero dependencies on Infrastructure or API layers.
3. **MassTransit Hybrid:** 
   - Use `IMediator` (MassTransit) for in-memory, decoupled module events.
   - Use `IPublishEndpoint` (SQS) for external/serverless background tasks (like video encoding).
4. **Validation:** All logic changes require corresponding unit or integration tests in the `tests/` directory.

## 🧩 Modular Infrastructure & DI
- **Module Lifecycle:** Every module MUST inherit from `AppModule` (implementing `IModule`).
  - `RegisterGlobal(IServiceCollection)`: For services shared across modules (e.g., global AWS clients, shared middleware).
  - `RegisterPrivate(IServiceCollection, ContainerBuilder)`: For internal module logic (Application/Infrastructure). Uses `Autofac.Populate(IServiceCollection)` to bridge MSDI and Autofac.
  - `Initialize(ILifetimeScope)`: Sets up the module's isolated scope after the application is built.
- **Service Discovery:** `Program.cs` dynamically scans for `AppModule` and `IEndpoint` implementations.
- **MassTransit & Messaging:** 
  - `AddMediator`: Used for in-memory messaging (Internal MediatR/MassTransit).
  - `AddMassTransit`: Used for external SQS-backed messaging.
  - **Convention:** Consumers with "sqs" in their name are registered for SQS; others are registered for the In-Memory Mediator.
- **Endpoints:** Feature endpoints must implement `IEndpoint` and are automatically mapped during startup.

## 📂 Key Source of Truth
- **Requirements:** `docs/PRD.md`
- **Features:** `docs/MVP-Score.md`
- **Roles/Access:** `docs/Roles.md`
- **Structure:** `docs/FolderStructure.md`

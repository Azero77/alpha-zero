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

## 📂 Key Source of Truth
- **Requirements:** `docs/PRD.md`
- **Features:** `docs/MVP-Score.md`
- **Roles/Access:** `docs/Roles.md`
- **Structure:** `docs/FolderStructure.md`

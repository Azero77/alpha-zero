# AlphaZero Identity & Access Management (IAM)

AlphaZero Identity is a high-performance, modular IAM framework designed for multi-tenant SaaS environments. It combines global identity (Cognito) with granular, resource-scoped permissions and context-aware security (Device Locking).

## 🚀 Getting Started
- **[Integration Guide](how-to/integrate-in-module.md)**: How to secure your module and endpoints.
- **[Authentication Flow](getting-started.md)**: Exchanging global tokens for tenant-scoped access.

## 🧠 Core Explanations
- **[Identity Layers](explanation/identity-layers.md)**: Understanding Global vs. Tenant vs. Contextual Identity.
- **[Policy Evaluation Logic](explanation/evaluation-logic.md)**: How the engine decides to Allow or Deny.
- **[Device-Aware Security](explanation/device-security.md)**: Fingerprinting, RSA Signatures, and Main Device locking.

## 🛠️ Task Guides (How-to)
- **[Authorize an Endpoint](how-to/authorize-endpoints.md)**: Using `AccessControl` in FastEndpoints.
- **[Secure Device Flow](how-to/secure-device-flow.md)**: Implementing signatures in your frontend.
- **[Manage Principals](how-to/manage-principals.md)**: Creating roles and assigning them to users.

## 📚 Technical Reference
- **[Core Entities](reference/entities.md)**: Deep dive into `TenantUser`, `Principal`, and `Assignments`.
- **[Policy Schema](reference/policy-schema.md)**: JSON structure, actions, resources, and conditions.
- **[Resource ARNs](concepts/resource-arns.md)**: The AlphaZero Resource Naming convention.
- **[System Roles](reference/system-roles.md)**: Pre-defined principal templates.

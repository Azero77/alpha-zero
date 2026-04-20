# 🔐 Identity & Access Management (IAM) Internals

This module implements a high-performance, multi-tenant Authorization system inspired by AWS IAM, optimized for the AlphaZero modular monolith. It balances global authentication (Cognito) with granular, resource-scoped permissions.

---

## 🏗️ The Three-Layer Identity Model

### 1. Global Identity (The "Person")
*   **Source:** Managed by AWS Cognito.
*   **Identifier:** `IdentityId` (the `sub` claim in the Cognito JWT).
*   **Role:** Proves identity across the entire AlphaZero ecosystem but carries zero platform permissions.

### 2. Tenant Identity (The "Anchor" - `TenantUser`)
*   **Entity:** `TenantUser` aggregate.
*   **Identifier:** `TenantUserId` (GUID).
*   **Link:** Tied to `IdentityId` + `TenantId`.
*   **Session State:** Owns the `ActiveSessionId` (GUID) used for **Single Device Enforcement**.
*   **Purpose:** The central anchor for all user-specific data within a specific academy (enrollments, progress, etc.).

### 3. Contextual Identity (The "Identity" - `Principal`)
*   **Entity:** `Principal` (inherits from `PrincipalTemplate`).
*   **Types:** 
    *   **IAM Principal:** A sub-identity with local `Username`/`PasswordHash` (e.g., a "Library Accountant" bot or staff account).
    *   **Role Template:** A static template (e.g., "Student") assigned to `TenantUsers` via `TenantUserPrinciaplAssignment`.

---

## 🗃️ Policy Architecture & Storage

Permissions are stored as **JSONB** to allow for dynamic, complex structures without the performance overhead of hundreds of join-table rows.

### 1. Inline Policies (`Principal.InlinePolicies`)
Stored directly on the `Principal` record. These are "Surgical Overrides" for a specific user.
```json
[
  {
    "Sid": "AllowSpecificVideo",
    "Effect": true,
    "Actions": ["video:Stream"],
    "Resources": ["az:video:T1:video/math-lesson-01"]
  }
]
```

### 2. Managed Policies (`ManagedPolicy`)
Global templates that can be attached to any `PrincipalTemplate`.
*   **TPT Mapping:** Uses Table-Per-Type inheritance. `Principals` and `PrincipalTemplates` share IDs, allowing a single join table (`PrincipalManagedPolicyAssignments`) to handle both roles and individual users.

---

## 📡 Authorization Evaluation Logic

The `PolicyEvaluatorService` follows a strict **Hierarchical Evaluation Flow**:

1.  **Explicit Deny:** If any policy (Inline or Managed) contains a matching action/resource with `Effect: false`, access is rejected immediately.
2.  **Explicit Allow:** 
    *   **Inline Check:** Scans the `Principal`'s custom JSON array.
    *   **Managed Check:** Scans joined policies. For roles, it uses the **Assignment Scope** (e.g., `az:courses:T1:course/101/*`) to bound the permissions.
3.  **Implicit Deny:** If no matches are found, the request is rejected (Zero Trust).

### Resource ARN Format
`az:{service}:{tenantId}:{resourcePath}`
*   Example: `az:courses:4192-543:course/calculus-101/lesson/derivatives`

### Resource Pattern Matching
*   **Wildcards:** `az:courses:*:*` matches everything in the courses module.
*   **Placeholders:** `az:courses:T1:course/{courseId}` (future-proofing for variable injection).

---

## 🔑 Login & Token Flows

### 1. Global to Tenant Exchange (STS Pattern)
Used when a student (Person) enters a specific Tenant (Academy).
1.  User authenticates with Cognito $\rightarrow$ Platform JWT.
2.  User calls `POST /identity/auth/exchange-tenant-token`.
3.  Server verifies Cognito `sub`, refreshes `ActiveSessionId` in DB, and issues an **AlphaZero Scoped JWT**.

### 2. IAM Principal Login
Used for staff or bots with local credentials.
1.  User calls `POST /identity/auth/login-principal` with `Username`, `Password`, and `TenantId`.
2.  Server verifies `PasswordHash`, generates a session, and issues a **Scoped JWT** with `auth_method: Principal`.

---

## 🛠️ Developer Implementation Examples

### Example: Defining a Scoped Requirement in an Endpoint
```csharp
// Inside a FastEndpoint
public override void Configure()
{
    Get("/courses/{CourseId}/videos/{VideoId}");
    // Checks if current user has 'video:Stream' permission for this specific resource path
    this.AccessControl("video:Stream", req => ResourceArn.ForVideo(TenantId, req.VideoId));
}
```

### Example: Manual Authorization in a Command
```csharp
var result = await _evaluator.Authorize(
    userId, 
    tenantId, 
    "course/math-101", 
    ResourceType.Courses, 
    "course:Edit", 
    AuthorizationMethod.TenantUser.ToString());
```

---

## 🚀 Performance Optimization
*   **JSONB Queries:** The `PrincipalRepository` includes JSONB columns in a single fetch, preventing N+1 queries.
*   **Unique Constraints:** Strictly enforced for `(TenantUserId, PrincipalTemplateId, ResourceArn)` to prevent role explosion in the assignments table.

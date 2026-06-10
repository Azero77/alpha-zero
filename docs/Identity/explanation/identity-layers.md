# The Three Layers of Identity

AlphaZero uses a layered identity model to decouple global authentication from local authorization and contextual permissions.

## Layer 1: Global Identity (The "Person")
**Entity:** Managed by AWS Cognito.
**Identifier:** `IdentityId` (the `sub` claim).

The "Person" layer proves *who* someone is across the entire platform. It doesn't grant any permissions within an Academy. A person can belong to multiple Academies with different roles in each.

## Layer 2: Tenant Identity (The "Anchor")
**Entity:** `TenantUser`.
**Identifier:** `TenantUserId`.

When a "Person" joins an Academy, a `TenantUser` record is created. This is the central anchor for that user's data within the tenant (e.g., student progress, completed assessments). It also holds the user's registered devices.

## Layer 3: Contextual Identity (The "Principal")
**Entity:** `Principal` / `TenantUserPrincipalAssignment`.

The "Principal" layer defines *what* a user can do. This layer is highly dynamic. 
- A user might be a **Student** in "Course A" (scoped permission).
- The same user might be a **Teacher** in "Course B" (scoped permission).
- A staff member might have an **Administrator** role across the entire Tenant (tenant-scoped).

---

## The Authorization Flow

When a request reaches the server, the `IdentityModule` performs the following "Promotion" flow:

1. **Extraction**: Extract `IdentityId` and `TenantId` from the incoming JWT.
2. **Context Building**: The `AuthorizationContextFactory` looks up the `TenantUser` and any active `TenantUserPrincipalAssignments` for the target resource.
3. **Promotion**: The system "promotes" the generic user to a collection of **Effective Policies** based on the assignment scope.
4. **Evaluation**: The `PolicyEvaluator` runs the target action (e.g., `courses:Edit`) against the effective policies.

## Why this model?
1. **Scalability**: We can have millions of students without creating millions of unique role records. We reuse the "Student" role principal and only create lightweight "Assignments".
2. **Flexibility**: We can easily grant "Surgical Overrides" by attaching an `InlinePolicy` to a specific user principal.
3. **Privacy**: Global identities (Cognito) are decoupled from tenant-specific data, making it easier to handle data isolation and white-labeling.

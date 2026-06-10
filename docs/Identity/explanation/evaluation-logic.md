# Policy Evaluation Logic

The AlphaZero policy engine follows a rigorous, deterministic flow to decide if a request is authorized. It is built on a **Zero Trust** model: everything is denied unless explicitly allowed.

## The Evaluation Algorithm

When an authorization check is triggered (e.g., `courses:Edit` on `az:courses:T1:course/123`), the engine follows these steps:

### 1. Context Gathering
The `AuthorizationContextFactory` gathers:
- The Subject's `TenantUserId` or `PrincipalId`.
- The `AuthenticationMethod` (TenantUser or Principal).
- The `DeviceId` and signature metadata.
- All applicable **Effective Policies**.

### 2. Finding Effective Policies
- **For IAM Principals**: All `InlinePolicies` and `ManagedPolicies` attached to the principal.
- **For TenantUsers**: The system finds the `TenantUserPrincipalAssignment` that best matches the target Resource ARN. It then takes all policies from the assigned Principal and scopes them to that resource.

### 3. Statement Matching
The engine iterates through every statement in every effective policy. A statement matches if:
- **Action Match**: The required permission (e.g., `courses:Edit`) matches one of the statement's `Actions` (supporting wildcards like `courses:*`).
- **Resource Match**: The target Resource ARN matches one of the statement's `Resources` (using glob matching).
- **Condition Match**: If a `Condition` block is present, it must evaluate to `true`.

### 4. The Decision Matrix

| Scenario | Decision |
| :--- | :--- |
| Any matching statement has `Effect: Deny` | **Explicit Deny** (Immediate Stop) |
| At least one matching statement has `Effect: Allow` | **Explicit Allow** |
| No matching statements found | **Implicit Deny** |

---

## Permission Scoping in Assignments

One of the most powerful features of the engine is how it handles assignments.

When a role like `Student` is assigned to a user for a specific course (e.g., `az:courses:T1:course/101`), the engine automatically appends `/*` to the resource scope. 

This means a policy statement in the `Student` role that grants `courses:View` on `az:courses:T1:course/{CourseId}` will only match if the `{CourseId}` segment matches `101` or a child resource of `101`.

## Performance
Evaluation is performed in-memory after a single batch fetch of the user's policies. JSONB columns are used to store the statements, ensuring that even complex policy sets are retrieved in a single database round-trip.

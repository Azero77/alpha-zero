# The Policy Model

AlphaZero uses a policy-based authorization model where permissions are defined as a collection of **Statements**. These statements are evaluated in real-time to determine if a request should be allowed or denied.

## Anatomy of a Policy Statement

A `PolicyStatement` is a single rule that defines access.

```json
{
  "Sid": "AllowStreaming",
  "Effect": true, 
  "Actions": ["video:Stream"],
  "Resources": ["az:video:T1:video/*"],
  "Condition": null
}
```

- **Sid (Statement ID)**: An optional identifier for the rule.
- **Effect**: `true` for Allow, `false` for Deny.
- **Actions**: A list of permissions (e.g., `video:Stream`, `course:Edit`).
- **Resources**: A list of `ResourcePattern` strings defining the scope.
- **Condition**: Optional logic that must be met (e.g., `MainDeviceOnly`).

---

## Types of Policies

### 1. Managed Policies
Managed Policies are standalone templates that can be attached to multiple roles or users. They are ideal for reusable sets of permissions (e.g., "TeacherAccess", "StudentAccess").

### 2. Inline Policies
Inline Policies are stored directly on a `Principal` record. They are used for surgical overrides or unique permissions that shouldn't be shared.

---

## Evaluation Logic

The `PolicyEvaluationEngine` follows a strict hierarchical flow to reach a decision:

### 1. Explicit Deny (Priority)
If **any** matching statement has `Effect: false`, the request is immediately rejected. **Deny always wins.**

### 2. Explicit Allow
If no Deny matches, the engine looks for an Allow statement (`Effect: true`).
- First, it checks **Inline Policies**.
- Then, it checks all **Managed Policies** attached to the user or their roles.

### 3. Implicit Deny (Zero Trust)
If no matching statements are found (neither Allow nor Deny), the request is rejected.

---

## Conditions

Conditions add a layer of logic beyond simple Action/Resource matching.

### Example: Main Device Only
A policy might allow streaming only if the request comes from the user's "Main Device".

```json
{
  "Effect": true,
  "Actions": ["video:Stream"],
  "Resources": ["*"],
  "Condition": {
    "Type": "MainDeviceOnly",
    "Properties": {}
  }
}
```

The `ConditionEvaluatorService` handles the execution of this logic, comparing request metadata (fingerprints, IP, etc.) against stored user state.

---

## Storage: JSONB vs Relational
To maintain high performance in a multi-tenant environment, AlphaZero stores policy statements as **JSONB** columns. This allows:
- **Complex Hierarchies**: Roles can be assigned with specific resource scopes without creating hundreds of join-table rows.
- **Atomic Updates**: Policies can be updated in a single database operation.
- **Efficient Retrieval**: Using EF Core with JSONB support, the entire authorization context is retrieved in a single query.

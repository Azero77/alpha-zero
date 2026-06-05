# AlphaZero Identity & Access Management (IAM)

The AlphaZero Identity module is a high-performance, modular IAM framework designed for multi-tenant SaaS environments. It moves beyond simple RBAC (Role-Based Access Control) into a dynamic **ABAC/PBAC hybrid** model, allowing for fine-grained, context-aware security policies.

---

## 📖 Fundamental Concepts

### 1. The Principal
A **Principal** is the security anchor. It can be a physical **User** or a reusable **Role**.
*   **TenantUser**: A global identity (e.g., from Cognito) mapped into a specific Tenant.
*   **Principal (Entity)**: Holds specific security attributes, credentials (for service accounts), and policies.

### 2. ARNs (AlphaZero Resource Names)
Resources are identified by a strict, segmented naming convention.
**Format:** `az:<service>:<tenantId>:<resource-path>`

| ARN Segment | Description | Example |
| :--- | :--- | :--- |
| `service` | The module owning the resource. | `courses`, `library`, `video` |
| `tenantId` | The unique ID of the tenant. | `7f2a...` or `global` |
| `resource-path` | The specific path to the item. | `course/math-101`, `user/ali` |

### 3. Policies
Policies are JSON-based documents that define permissions.
*   **Managed Policies**: Reusable templates (e.g., "StudentAccess") that can be shared across thousands of users.
*   **Inline Policies**: Specific permissions attached directly to a single Principal for one-off overrides.

---

## 🛡️ The Policy Engine

AlphaZero uses an **Implicit Deny** strategy. Access is only granted if a statement explicitly allows it, and no matching statement explicitly denies it.

### Statement Structure
```json
{
  "Sid": "AllowCourseStreaming",
  "Effect": true, 
  "Actions": ["video:Stream"],
  "Resources": ["az:video:{tenantId}:course/math-*"],
  "Condition": { ... }
}
```

### Evaluation Hierarchy
1.  **Explicit Deny**: Any matching statement with `Effect: false` results in immediate `Forbidden`.
2.  **Explicit Allow**: At least one matching statement must have `Effect: true`.
3.  **Condition Pass**: If an "Allow" statement has a condition, it **must** evaluate to true.
4.  **Final Verdict**: If no statements match, the result is `Forbidden`.

---

## 🧠 Dynamic Conditions (ABAC)

Conditions allow you to write "Smart Policies" that respond to the request context.

### Condition Nodes
The condition engine supports a recursive tree structure:
*   **StatementNode**: The leaf comparison (e.g., `Value == "X"`).
*   **AndNode / OrNode**: Logical groupings of multiple conditions.
*   **NotNode**: Inverts child logic.

### Variable References (`$`)
You can reference properties from the `AuthorizationContext` dynamically. This is critical for preventing horizontal privilege escalation.

**Example: "A user can only edit their own profile"**
```json
{
  "Property": "ResourcePath",
  "Operator": "StringEquals",
  "Value": "user/$Id" 
}
```
*Here, `$Id` is replaced by the current user's ID during evaluation.*

---

## 🚀 Usage Guide

### 1. Defining a Managed Policy
Managed policies are created globally and built for specific tenants at runtime.

```json
// POST /identity/policies/managed
{
  "name": "TeacherRole",
  "statements": [
    {
      "sid": "ManageOwnCourses",
      "actions": ["courses:Edit", "courses:Publish"],
      "effect": true,
      "resources": ["az:courses:{tenantId}:*"],
      "condition": {
        "type": "Statement",
        "property": "PrincipalId",
        "operator": "StringEquals",
        "value": "$ResourceId"
      }
    }
  ]
}
```

### 2. Scoped Assignments (The "Enrollment" Pattern)
In AlphaZero, we don't just "give a role." we "assign a role for a resource."

When a student enrolls in `Course A`, we create a `TenantUserPrincipalAssignment`:
*   **User**: Student Ali
*   **Role**: StudentTemplate
*   **Scope**: `az:courses:tenant-1:course/math-101`

The engine will then grant the permissions defined in the `StudentTemplate` **only** for the math course.

---

## 🛠️ Condition Operators Reference

| Operator | Type | Description |
| :--- | :--- | :--- |
| `StringLike` | String | Supports wildcards (e.g., `math-*`). |
| `NumericLessThan` | Number | Compares numeric values (useful for age/level). |
| `DateGreaterThan` | Date | Useful for time-expiry or scheduled access. |
| `In` | Array | Checks if a value exists in a provided list. |
| `Bool` | Boolean | Checks for true/false flags in context. |

---

## 📝 Best Practices

> [!IMPORTANT]
> **Always use `{tenantId}` placeholders** in managed policies to ensure the policy remains tenant-isolated when applied.

> [!TIP]
> **Order doesn't matter for Deny**: Since any Deny statement triggers a failure, you don't need to worry about the order of statements in your JSON.

> [!WARNING]
> **Condition Errors**: If a condition fails to evaluate (e.g., referencing a property that doesn't exist), the engine treats the entire statement as a **Deny** for safety.

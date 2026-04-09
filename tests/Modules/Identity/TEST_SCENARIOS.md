# 🪐 AlphaZero Identity Module: IAM Integration Test Scenarios

This document outlines the exhaustive integration test suite for the Identity Module. These tests verify the interaction between the Domain logic, the EF Core JSONB persistence, and the Policy Evaluation engine using a real PostgreSQL database.

## 🏛️ 1. Principal & Inline Policy Persistence (JSONB)

| Scenario | Goal | Expected Result |
| :--- | :--- | :--- |
| **Save Principal with 0 Policies** | Verify basic persistence. | Principal saved; `InlinePolicies` returns empty list (not null). |
| **Save Principal with 1 Inline Policy** | Verify JSONB serialization. | Principal saved; JSON column contains correct Action/Effect/Resource. |
| **Update Principal (Add Policy)** | Verify JSONB update logic. | New policy appended to the existing JSON array in DB. |
| **Update Principal (Remove Policy)** | Verify partial JSONB deletion. | Target policy removed from array; others remain intact. |
| **Save Statement with Condition** | Verify `JsonElement` mapping. | Custom condition JSON is preserved and retrievable. |

## 🏛️ 2. Managed Policy Templates

| Scenario | Goal | Expected Result |
| :--- | :--- | :--- |
| **Create Managed Policy Template** | Verify template storage. | Template saved with `PolicyTemplateStatement` serialized as JSONB. |
| **Delete Template** | Verify cleanup. | Template removed; should fail if active assignments exist (optional based on FK). |

## 🏛️ 3. Principal-Policy Assignments (Many-to-Many)

| Scenario | Goal | Expected Result |
| :--- | :--- | :--- |
| **Attach Template to Principal** | Verify Join Table. | New row in `PrincipalPolicyAssignments` table. |
| **Detach Template from Principal** | Verify Join Table cleanup. | Row removed; Principal and Template entities remain in DB. |
| **Fetch All Policies for Principal** | Verify Repository Join. | Returns list of both Inline (parsed JSON) and Managed (joined rows). |

## 🏛️ 4. The "IAM Engine" End-to-End (Integration)

| Scenario | Goal | Expected Result |
| :--- | :--- | :--- |
| **Authorize via Inline Allow** | End-to-end check. | Service loads Principal from DB, parses JSON, returns `Success`. |
| **Authorize via Managed Policy** | End-to-end check. | Service performs DB Join, evaluates template, returns `Success`. |
| **Deny via Cross-Tenant Resource** | Security check. | Service builds ARN with wrong Tenant; returns `Forbidden`. |
| **Deny via Explicit Statement** | Security check. | Service finds `Effect: false` in DB; short-circuits and returns `Forbidden`. |
| **Enforce Scope URN from DB** | Security check. | Principal scoped to `Course/A` in DB cannot access `Course/B` even with broad template. |

## 🏛️ 5. Performance & Indexing (Sanity)

| Scenario | Goal | Expected Result |
| :--- | :--- | :--- |
| **Query Principals by ResourceId** | Verify Index usage. | Correct list of principals returned for a specific Course/Lesson. |

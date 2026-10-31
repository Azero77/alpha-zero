# Policy Schema Reference

AlphaZero uses a JSON-based policy system inspired by AWS IAM. Policies are stored as JSONB in the database, allowing for high-performance retrieval and complex evaluation logic without massive join tables.

## Policy Structure

A policy consists of one or more **Statements**.

```json
{
  "Version": "2026-05-31",
  "Statements": [
    {
      "Sid": "AllowStreaming",
      "Effect": true,
      "Actions": ["video:Stream"],
      "Resources": ["az:video:T1:video/*"],
      "Condition": {
        "Type": "Statement",
        "Property": "DeviceId",
        "Operator": "IsMainDevice",
        "Value": true
      }
    }
  ]
}
```

### Statement Fields

| Field | Type | Description |
| :--- | :--- | :--- |
| `Sid` | `string` | **Statement ID**. A unique identifier for the statement within the policy. |
| `Effect` | `bool` | `true` for **Allow**, `false` for **Deny**. AlphaZero uses an implicit deny strategy; an explicit Deny always overrides an Allow. |
| `Actions` | `List<string>` | A list of permissions. Format: `service:action`. Supports wildcards (e.g., `courses:*`, `*:*`). |
| `Resources` | `List<string>` | A list of [Resource ARNs](../concepts/resource-arns.md) or patterns. Supports wildcards (e.g., `az:courses:T1:course/*`). |
| `Condition` | `Object` | (Optional) A logical tree of conditions that must be met for the statement to apply. |

---

## Condition Operators

Conditions allow for context-aware authorization. The `ConditionEvaluator` supports logical nodes and various operators.

### Logical Nodes
- **And**: All child conditions must be true.
- **Or**: At least one child condition must be true.
- **Not**: Inverts the result of the child condition.
- **Reference**: Delegates evaluation to a named condition definition stored in the `ConditionDefinitions` table (re-usable named condition trees).

### Comparison Operators

| Operator | Supported Types | Description |
| :--- | :--- | :--- |
| `StringEquals` | `string` | Case-insensitive equality. |
| `StringLike` | `string` | Supports `*` as a wildcard. |
| `NumericGreaterThan` | `number` | Numeric comparison. |
| `DateLessThan` | `date` | Date/Time comparison. |
| `Bool` | `bool` | Boolean equality. |
| `In` | `array` | Checks if a value exists in a provided list. |

### Specialized Operators

#### `IsMainDevice`
The most powerful security operator in AlphaZero. It verifies that:
1. The request is coming from the user's registered **Main Device**.
2. The request carries a valid **RSA Signature** signed by the device's private key.
3. The signature includes a timestamp to prevent **Replay Attacks** (5-minute window).

#### `AccessTicketValid`
Course-entitlement operator (issue #34): verifies the **access-window ticket** sent in the
dedicated `X-Access-Ticket` header (NOT the JWT). The ticket is a signed, stateless capability
minted by the Courses stateful gate; this operator is **course-agnostic and reads NO database**:
1. The header must be present and the ticket must verify against the shared signer
   (**access ticket**, dedicated `AccessTicket:Secret`, HMAC-SHA256).
2. The ticket must be bound to the acting actor (`uid` = context.Id, `tid` = context.TenantId).
3. The ticket must not be expired (`exp`, ~2h re-mint bound) and its frozen **access window**
   (`windowStart`/`windowEnd`, the "they bought N days" entitlement) must still be open.

Every failing shape — missing header, malformed/unverifiable token, wrong key, foreign ticket,
elapsed window — **fails closed** (deny). Policy statement shape: `{ "Property": "Id", "Operator": "AccessTicketValid", "Value": true }` (the `Value` is ignored; the operator is header-driven).

---

## Policy Types

### 1. Managed Policies
Re-usable, named policy documents (e.g., `AdministratorAccess`, `StudentAccess`). These are typically attached to **Role Principals**.

### 2. Inline Policies
Surgical overrides attached directly to a specific **Principal**. Useful for granting temporary access or exceptions without creating new roles.

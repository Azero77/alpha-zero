# Resource ARNs & Patterns

AlphaZero identifies every protected asset using a **Resource ARN** (AlphaZero Resource Name). This unified string format allows the IAM module to evaluate permissions across different modules without knowing the underlying database structure.

## Resource ARN Format

A concrete Resource ARN represents a single, unique resource. It **must not** contain wildcards.

**Format**: `az:{service}:{tenantId}:{resourcePath}`

| Part | Description | Example |
| :--- | :--- | :--- |
| `az` | Static prefix for AlphaZero. | `az` |
| `service` | The module owning the resource. | `courses`, `video`, `library` |
| `tenantId` | The GUID of the Academy (or `global`). | `3fa85f6...` |
| `resourcePath` | Hierarchical path to the resource. | `course/math-101/lesson/1` |

### Examples

- **A Specific Course**: `az:courses:T1:course/calculus-101`
- **A Video**: `az:video:T1:video/math-intro`
- **A User Profile**: `az:identity:global:user/U123`

---

## Resource Patterns

Resource Patterns define a **scope** of permissions. They can contain wildcards and placeholders.

**Format**: `az:{service}:{tenantId}:{resourcePathPattern}`

### Wildcards (`*`)
Wildcards can be used in any part of the pattern to match multiple values.

- **Everything in Courses**: `az:courses:*:*`
- **All resources in Tenant T1**: `az:*:T1:*`
- **All lessons in a specific course**: `az:courses:T1:course/math-101/lesson/*`

### Placeholders (`{key}`)
Placeholders allow for dynamic matching based on the request context. This is useful for "Self" permissions.

- **Own User Data**: `az:identity:global:user/{userId}`
- **Enrollment Access**: `az:courses:T1:enrollment/{enrollmentId}`

---

## Technical Implementation

### `ResourceArn` Class
The `ResourceArn` class is the source of truth for concrete ARNs. It provides static factory methods for common resources:

```csharp
var courseArn = ResourceArn.ForCourse(tenantId, courseId);
// Returns "az:courses:3fa85...:course/..."
```

### `ResourcePattern` Class
The `ResourcePattern` class handles matching logic. It supports regex-based evaluation of placeholders and trailing wildcards.

```csharp
var pattern = ResourcePattern.Create("az:courses:T1:course/*").Value;
var arn = ResourceArn.ForCourse(tenantId, courseId);

bool isMatch = pattern.IsMatch(arn); // True
```

## Best Practices

1.  **Be Granular**: Grant permissions to specific resource paths whenever possible.
2.  **Use Trailing Wildcards**: Use `path/*` instead of just `*` if you want to include all sub-resources.
3.  **Validate ARNs**: Always use the `ResourceArn` factory methods to ensure consistent formatting.

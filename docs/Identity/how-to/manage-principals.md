# How to Manage Principals & Roles

AlphaZero provides a flexible system for creating roles and assigning them to users at various levels of granularity.

## 1. Creating a Principal Template (Role)
Roles are created as **Principal Templates**. These templates have no predefined scope; they are generic containers for `ManagedPolicies`.

```bash
# Example: Creating a 'CourseEditor' role
POST /identity/principals
{
  "Username": "CourseEditorTemplate",
  "Name": "Course Editor",
  "PrincipalType": "Role",
  "PrincipalScope": null
}
```

## 2. Attaching Policies
Once a principal is created, you attach `ManagedPolicies` to it.

```bash
# Attach the 'CourseWorkerAccess' managed policy to the new role
POST /identity/principals/{PrincipalId}/policies/managed/{ManagedPolicyId}
```

## 3. Assigning a Principal to a User
This is where you grant actual access by "binding" a principal to a user for a specific resource.

```bash
# Assign 'CourseEditor' to 'Ali' for 'Math 101'
POST /identity/principals/assignments
{
  "TenantUserId": "...",
  "PrincipalId": "...",
  "ResourceArn": "az:courses:T1:course/math-101"
}
```

### Effective Permissions
Once assigned, Ali will have all permissions defined in the `CourseEditor` role, but **ONLY** when interacting with `az:courses:T1:course/math-101` or its children.

---

## 4. Granting Surgical Overrides (Inline Policies)
Sometimes you need to give a specific user an extra permission without modifying the global role. Use **Inline Policies**.

```bash
# Give Ali permission to delete math-101 (an exception)
POST /identity/principals/{PrincipalId}/policies/inline
{
  "PolicyName": "DeleteException",
  "Statements": [
    {
      "Sid": "AllowDelete",
      "Effect": true,
      "Actions": ["courses:Delete"],
      "Resources": ["az:courses:T1:course/math-101"]
    }
  ]
}
```

## 5. Revoking Access
To revoke access, simply remove the assignment.

```bash
DELETE /identity/principals/assignments/{AssignmentId}
```

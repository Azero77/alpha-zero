# How to Authorize Endpoints

In AlphaZero, most authorization logic is handled at the API layer using the `AccessControl` extension for FastEndpoints. This keeps your domain logic clean and ensures consistent enforcement.

## Using `AccessControl`

The `AccessControl` extension allows you to declare exactly what permission and resource are required for an endpoint.

### Example: Basic Tenant-Scoped Access
If an action only requires a general permission within a tenant (e.g., creating a course), use the `ResourceArn.ForTenant` helper.

```csharp
public override void Configure()
{
    Post("/courses");
    // Checks for 'courses:Create' permission for the current Academy
    this.AccessControl("courses:Create", (req, tenantId) => ResourceArn.ForTenant(tenantId));
}
```

### Example: Resource-Specific Access
If an action targets a specific resource (e.g., editing a specific course), resolve the ARN from the request data.

```csharp
public override void Configure()
{
    Put("/courses/{Id}");
    // Checks for 'courses:Edit' permission for the specific Course ARN
    this.AccessControl("courses:Edit", (req, tenantId) => ResourceArn.ForCourse(tenantId, req.Id));
}
```

### Example: Global Access (No Tenant)
For global identity management, use the `ForUser` helper.

```csharp
public override void Configure()
{
    Get("/users/{UserId}/profile");
    this.AccessControl("identity:ViewProfile", req => ResourceArn.ForUser(req.UserId));
}
```

---

## Under the Hood: The Evaluation Process

When a request hits an endpoint with `AccessControl`:

1.  **Requirement Metadata**: The extension adds an `AccessControlRequirement` to the endpoint metadata.
2.  **Authorization Middleware**: The `AuthorizationHandler` interceptor triggers.
3.  **Context Resolution**:
    - The `CurrentTenant` is resolved from the header.
    - The `IdentityId` is resolved from the JWT.
    - The `ResourceArn` is resolved by executing the factory function provided in `AccessControl`.
4.  **Evaluation**: The `PolicyEvaluatorService` is called to perform the [Hierarchical Evaluation](concepts/policy-model.md#evaluation-logic).
5.  **Result**: If allowed, the request proceeds to the handler. If denied, a `403 Forbidden` response is returned.

---

## Manual Authorization in Commands

Sometimes authorization depends on the result of a database query or complex domain logic that isn't available at the API configuration level. In these cases, you can inject `IPolicyEvaluatorService` into your Application layer.

```csharp
public class EditCourseCommandHandler(IPolicyEvaluatorService evaluator) 
{
    public async Task<ErrorOr<Success>> Handle(EditCourseCommand command)
    {
        var result = await evaluator.Authorize(new AuthorizationContext(
            command.UserId,
            command.TenantId,
            "course:Edit",
            ResourceType.Courses,
            $"course/{command.CourseId}",
            "TenantUser" // The authentication method
        ));

        if (result.IsError) return result;
        
        // Proceed with logic...
    }
}
```

## Best Practices

- **Prefer API-Level Control**: Always use `this.AccessControl()` in your Endpoint configuration unless the resource ARN is truly dynamic and unpredictable at the start of the request.
- **Fail Fast**: Authorization should be the first check in your request pipeline.
- **Consistency**: Use the `ResourceArn` factory methods to ensure your ARNs match the patterns defined in your policies.

# How to Integrate Identity in Your Module

This guide shows how to secure your module's endpoints and commands using the AlphaZero Identity system.

## 1. Securing Endpoints (FastEndpoints)

The easiest way to enforce authorization is via the `this.AccessControl()` extension in your endpoint configuration.

### Example: Adding a Lesson to a Course
```csharp
public class AddLessonEndpoint : Endpoint<AddLessonRequest>
{
    public override void Configure()
    {
        Post("/courses/{CourseId}/sections/{SectionId}/lessons");
        
        // ENFORCEMENT:
        // Permission required: "courses:Edit"
        // Target Resource: The specific course ARN
        this.AccessControl("courses:Edit", (req, tenantId) => 
            ResourceArn.ForCourse(tenantId, req.CourseId));
            
        Description(d => d.WithTags("Courses"));
    }
}
```

### How it works:
1. **Permission**: `courses:Edit` is the action segment.
2. **Resource Resolver**: A lambda that returns the `ResourceArn`. The system automatically provides the `tenantId` from the request context.
3. **Evaluation**: The middleware will:
   - Identify the current `TenantUser` from the JWT.
   - Look for a `TenantUserPrincipalAssignment` that matches the course ARN (or its parent).
   - Evaluate all associated policies to find an `Allow` effect for `courses:Edit`.

---

## 2. Using Module-to-Module Communication

Modules should not depend on `IdentityModule` directly. Instead, they use the `IIdentityModule` interface or MassTransit integration events.

### Example: Assigning a Role via Saga
When a student enrolls in a course, the `Courses` module emits an event. The `Identity` module listens and creates the assignment.

```csharp
public class AssignStudentRoleConsumer : IConsumer<EnrollmentCreatedEvent>
{
    private readonly IIdentityModule _identityModule;

    public async Task Consume(ConsumeContext<EnrollmentCreatedEvent> context)
    {
        var studentRoleId = Guid.Parse("00000000-0000-0000-0000-100000000002");
        
        var command = new AssignPrincipalToUserCommand(
            context.Message.UserId, 
            studentRoleId, 
            context.Message.CourseArn);

        await _identityModule.Send(command);
    }
}
```

---

## 3. Context-Aware Requirements (Device Locking)

If your feature requires high security (e.g., content consumption), you don't need to change your endpoint code. You simply update the **Managed Policy** for that role to include a `Condition`.

### Secure Policy Example:
```json
{
  "Sid": "StreamingRequiresMainDevice",
  "Effect": true,
  "Actions": ["video:Stream"],
  "Resources": ["az:video:T1:video/*"],
  "Condition": {
    "Operator": "IsMainDevice",
    "Value": true
  }
}
```

With this policy, any request to `video:Stream` will fail with `403 Forbidden` unless the user provides a valid device signature from their **Main Device**.

---

## 4. Manual Authorization Check
If you need to check permissions inside a service (not an endpoint), use `IPolicyEvaluatorService`.

```csharp
public class MyService(IPolicyEvaluatorService authService)
{
    public async Task DoSomething(AuthorizationContext context)
    {
        var result = await authService.Authorize(context);
        if (result.IsError) throw new UnauthorizedAccessException();
    }
}
```

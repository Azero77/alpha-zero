# Implementation Plan: AuthorizationContextProvider Refactoring & Completion

## Architectural Evaluation: What We Think of the New Design

The core architectural direction of **moving metadata and claim extraction into `AuthorizationContextProvider` without database queries** is a **major improvement** for AlphaZero for three reasons:

1. **Zero Database Overhead on Context Resolution**:
   Previously, `IAuthorizationContextFactory.Create` queried `ICurrentTenantUserRepository.GetCurrentUser()` and `ITenantUserPrincipalAssignmentRepository.GetActiveAssignment(...)` on every request, only for `TenantUserAuthorizationStrategy` to query assignments *again* during policy evaluation. Extracting user and device identity directly from JWT claims (`sub`, `identity_id`, `tid`, `device_id`, etc.) makes context creation 100% in-memory and instantaneous.
2. **Strong Typing via Polymorphism**:
   Specializing `AuthorizationContext` into `TenantUserAuthorizationContext` and `PrincipalUserAuthorizationContext` provides type safety for device fingerprints (`MainDeviceId`, `PublicKey`, `DevicePlatform`) and principal metadata (`PrincipalScope`, `IsManaged`) needed by condition evaluators without resorting to dictionary bags or casting.
3. **Clean Endpoint Metadata Encapsulation**:
   Having the provider inspect `HttpContext.GetEndpoint()?.Metadata` encapsulates endpoint requirements (`RequiredPermission`, `ResourceArn`) directly into the context creation pipeline.

---

### Critical Gaps in the Current Draft to Fix

> [!WARNING]
> **1. Global IAMPreprocessor Route Blocking:**
> Because `IAMPreprocessor` is registered as an `IGlobalPreProcessor`, it runs on **all** endpoints (including public/anonymous endpoints like `/auth/login`, `/health`, and `/courses/{id}/overview`). Without a guard checking whether the endpoint has `AccessControl` requirements, anonymous callers are sent HTTP 403 Forbidden on public routes.
>
> **2. Imperative / Dynamic Authorization in PreRequestHandlers:**
> For endpoints configured with `AllowAnonymous()` (e.g. `GET /courses/{id}/overview`), the endpoint has no `AccessControl` metadata. If a course is Draft, `GetCourseOverviewPreRequestHandler` must check if the caller has `"courses.overview:Manage"` for `ResourceArn.ForCourse(...)`. Since endpoint metadata is null, the handler cannot rely on endpoint metadata. The ambient `AuthorizationContext` must support clean, immutable resource scoping (e.g. `context.ForResource(permission, arn)`).
>
> **3. Incomplete Stubs & Compilation Failures:**
> - `AuthorizationContext.Empty` does not exist.
> - `CreatePrincipalFromClaims` and `CreateTenantUserFromClaims` in [IdentityModelClaimsExtensions.cs](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/Modules/Identity/Application/Auth/Extensions/IdentityModelClaimsExtensions.cs) are empty stubs.
> - `CreatePrincipalAuthContext` and `CreateTenantUserAuthContext` in [AuthorizationContextProvider.cs](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/Modules/Identity/Infrastructure/Services/AuthorizationContextProvider.cs) are empty stubs.
> - `IAuthorizationContextProvider` is missing `CurrentAuthorizationContext` needed by [PreRequestBehavior.cs](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/AlphaZero.Shared/Application/PreRequestBehavior.cs) and [RedeemCodeCommandHandler.cs](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/Modules/Library/Application/RedeemCode/RedeemCodeCommandHandler.cs).
> - `TenantUserAuthorizationContext.DeviceId` hides parent `AuthorizationContext.DeviceId`.
> - Unused DB repositories are still injected into `AuthorizationContextProvider`.
> - [DependencyInjection.cs](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/Modules/Identity/Infrastructure/DependencyInjection.cs) registers the old class name `AuthorizationContextFactory`.

---

## Architecture & Request Flow

```mermaid
sequenceDiagram
    autonumber
    actor Client
    participant FastEndpoints as FastEndpoints Pipeline
    participant IAMPreprocessor as IAMPreprocessor
    participant Provider as IAuthorizationContextProvider
    participant MediatR as MediatR (PreRequestBehavior)
    participant PreHandler as PreRequestHandler (Courses)
    participant Evaluator as IPolicyEvaluatorService

    Client->>FastEndpoints: HTTP Request
    FastEndpoints->>IAMPreprocessor: PreProcessAsync(context, ct)
    
    alt Endpoint Has AccessControl(...) Metadata
        IAMPreprocessor->>Provider: Get(ct)
        Provider->>Provider: Parse Claims & Endpoint Metadata (In-Memory)
        Provider-->>IAMPreprocessor: AuthorizationContext (RequiredPermission, TargetArn set)
        IAMPreprocessor->>Evaluator: Authorize(context)
        alt Policy Denied
            Evaluator-->>IAMPreprocessor: Error.Forbidden
            IAMPreprocessor-->>Client: 403 Forbidden
        end
    else Endpoint Without AccessControl(...) (e.g. AllowAnonymous)
        note over IAMPreprocessor: Skips authorization; passes through
    end

    FastEndpoints->>MediatR: Send(Query / Command)
    MediatR->>Provider: Read CurrentAuthorizationContext / Get(ct)
    MediatR->>PreHandler: PreHandle(Request, context, ct)

    alt PreHandler Requires Dynamic Check (e.g. Draft Course)
        note over PreHandler: context.ForResource("courses.overview:Manage", courseArn)
        PreHandler->>Evaluator: Authorize(scopedContext)
        alt Not Authorized
            PreHandler-->>MediatR: Error.NotFound (Masks existence)
            MediatR-->>Client: 404 Not Found
        end
    end

    MediatR->>FastEndpoints: Success Handler Result
    FastEndpoints-->>Client: 200 OK
```

---

## Proposed Changes

### Component 1: `AlphaZero.Shared` (Domain & Contracts)

#### [MODIFY] [AuthorizationContext.cs](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/AlphaZero.Shared/Authorization/AuthorizationContext.cs)
- Fix warnings and property hiding:
  - Remove redundant `public string DeviceId { get; init; }` from `TenantUserAuthorizationContext` (already inherited from base).
  - Make `PublicKey` `public string? PublicKey { get; init; }`.
  - Add `required` modifiers or default initializers where appropriate.
- Add `public static readonly AuthorizationContext Empty = new() { AuthenticationMethod = string.Empty };` (or factory helper).
- Add immutable resource scoping helper:
  ```csharp
  public AuthorizationContext ForResource(string requiredPermission, ResourceArn arn) => this with
  {
      RequiredPermission = requiredPermission,
      ResourcePath = arn.ResourcePath,
      ResourceType = arn.Service,
      TenantId = arn.TenantIdString.Equals(ResourceArn.GlobalTenant, StringComparison.OrdinalIgnoreCase)
          ? (TenantId ?? Guid.Empty)
          : Guid.Parse(arn.TenantIdString)
  };
  ```

#### [MODIFY] [IAuthorizationContextFactory.cs](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/AlphaZero.Shared/Authorization/IAuthorizationContextFactory.cs)
- Update `IAuthorizationContextProvider`:
  ```csharp
  namespace AlphaZero.Shared.Authorization;

  public interface IAuthorizationContextProvider
  {
      AuthorizationContext? CurrentAuthorizationContext { get; }
      ErrorOr<AuthorizationContext?> Get(CancellationToken token = default);
  }
  ```

#### [MODIFY] [DTOs.cs (IAMPreprocessor)](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/AlphaZero.Shared/Authorization/DTOs.cs)
- In `IAMPreprocessor`: Guard against endpoints that do not specify access control:
  ```csharp
  var globalRequirement = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<GlobalAccessControlRequirement>();
  var tenantScopedRequirement = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<AccessControlWithTenantRequirement>();

  if (globalRequirement is null && tenantScopedRequirement is null) return;
  ```
- Retrieve context via `authorizationContextProvider.Get(ct)` and authorize with `evaluator.Authorize(authContextResult.Value)`.

#### [MODIFY] [PreRequestBehavior.cs](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/AlphaZero.Shared/Application/PreRequestBehavior.cs)
- Clean up context retrieval using `_authorizationContextProvider`:
  ```csharp
  var authContext = _authorizationContextProvider.CurrentAuthorizationContext;
  if (authContext == null)
  {
      var contextResult = _authorizationContextProvider.Get(cancellationToken);
      if (!contextResult.IsError)
      {
          authContext = contextResult.Value;
      }
  }
  ```

---

### Component 2: `Modules/Identity` (Provider Implementation & Claims)

#### [MODIFY] [IdentityModelClaimsExtensions.cs](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/Modules/Identity/Application/Auth/Extensions/IdentityModelClaimsExtensions.cs)
- Implement `CreatePrincipalFromClaims(this ClaimsPrincipal user)` by delegating to or mirroring [ClaimsExtensions.ToPrincipalDTO()](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/AlphaZero.Shared/Authorization/ClaimsExtensions.cs#L55).
- Implement `CreateTenantUserFromClaims(this ClaimsPrincipal user)` by delegating to or mirroring [ClaimsExtensions.ToTenantUserDTO()](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/AlphaZero.Shared/Authorization/ClaimsExtensions.cs#L8).

#### [MODIFY] [AuthorizationContextProvider.cs](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/Modules/Identity/Infrastructure/Services/AuthorizationContextProvider.cs)
- Remove unused constructor parameters `ICurrentTenantUserRepository` and `ITenantUserPrincipalAssignmentRepository`.
- Implement `CreatePrincipalAuthContext` and `CreateTenantUserAuthContext` using `CreatePrincipalFromClaims` and `CreateTenantUserFromClaims`.
- Support caching: if `CurrentAuthorizationContext != null`, return it immediately.
- Clean up `GetEndpointMetadata()` to correctly extract action and ARN from `GlobalAccessControlRequirement` and `AccessControlWithTenantRequirement`.

#### [MODIFY] [DependencyInjection.cs](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/Modules/Identity/Infrastructure/DependencyInjection.cs)
- Change registration:
  ```csharp
  services.AddScoped<IAuthorizationContextProvider, AuthorizationContextProvider>();
  ```

#### [DELETE] [AuthorizationContextFactory.cs](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/Modules/Identity/Infrastructure/Services/AuthorizationContextFactory.cs)
- Remove orphaned empty file.

#### [MODIFY] [PolicyEvaluatorService.cs](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/Modules/Identity/Domain/Services/PolicyEvaluatorService.cs)
- Support `Testing:BypassIAM` via injected `IConfiguration?`:
  ```csharp
  if (_configuration?.GetValue<bool>("Testing:BypassIAM") == true)
  {
      return Result.Success;
  }
  ```

---

### Component 3: `Modules/Courses` & `Modules/Library`

#### [MODIFY] [GetCourseOverviewPreRequestHandler.cs](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/Modules/Courses/Application/Courses/Queries/GetCourseOverview/GetCourseOverviewPreRequestHandler.cs)
- Remove unused `IAuthorizationContextProvider` dependency.
- For draft courses with an authenticated user:
  ```csharp
  var targetContext = context.ForResource("courses.overview:Manage", arn);
  var authResult = await _policyEvaluatorService.Authorize(targetContext);
  if (authResult.IsError)
  {
      return Error.NotFound("CourseOverview.NotFound", "Course overview not found.");
  }
  return Result.Success;
  ```

#### [MODIFY] [RedeemCodeCommandHandler.cs](file:///home/azero/Desktop/AlphaZeroLearningAcademy/src/alphazero-api/Modules/Library/Application/RedeemCode/RedeemCodeCommandHandler.cs)
- Rename injected field to `_authorizationContextProvider` and read `_authorizationContextProvider.CurrentAuthorizationContext?.DeviceId`.

---

### Component 4: Test Alignment

#### [MODIFY] [GetCourseOverviewPreRequestHandlerTests.cs](file:///home/azero/Desktop/AlphaZeroLearningAcademy/tests/Modules/Courses/Courses.UnitTests/Application/Courses/GetCourseOverviewPreRequestHandlerTests.cs)
- Update tests to test the decoupled handler without needing to mock `GetOrCreateCurrentContextAsync`.
- Verify anonymous draft course returns 404, authorized draft course returns Success, unauthorized draft course returns 404, and published course returns Success.

#### [MODIFY] [PreRequestBehaviorTests.cs](file:///home/azero/Desktop/AlphaZeroLearningAcademy/tests/Modules/Courses/Courses.UnitTests/Application/Courses/PreRequestBehaviorTests.cs)
- Update mock setup to `_authProvider.Get(Arg.Any<CancellationToken>())` and verify short-circuiting on ErrorOr.

#### [MODIFY] [RedeemCodeCommandHandlerTests.cs](file:///home/azero/Desktop/AlphaZeroLearningAcademy/tests/Modules/Library/Library.UnitTests/Application/RedeemCode/RedeemCodeCommandHandlerTests.cs)
- Update mock setup for `IAuthorizationContextProvider`.

---

## Verification Plan

### Automated Tests
1. **Build the Solution**:
   ```bash
   dotnet build ./AlphaZero.sln
   ```
2. **Unit Tests (Courses, Identity, Library)**:
   ```bash
   dotnet test tests/Modules/Courses/Courses.UnitTests/Courses.UnitTests.csproj
   dotnet test tests/Modules/Identity/Identity.UnitTests/Identity.UnitTests.csproj
   dotnet test tests/Modules/Library/Library.UnitTests/Library.UnitTests.csproj
   ```
3. **Integration Tests (CourseOverview)**:
   ```bash
   dotnet test tests/Modules/Courses/Courses.Tests.Integration/Courses.Tests.Integration.csproj --filter CourseOverviewTests
   ```

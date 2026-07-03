# E2E Test Readiness Assessment Report

## 1. Observation

### Frontend Compilation
We executed the command `npx tsc --noEmit` inside the `frontend-web` folder and observed the following compilation errors:
* **File**: `src/app/[tenant]/courses/[courseId]/page.tsx`
  * Line 51: `Property 'alphaZeroModulesCoursesPresentationCoursesCompleteItemCompleteItemEndpoint' does not exist on type '...'`
* **File**: `src/app/[tenant]/device-lock/page.tsx`
  * Line 19: `Property 'alphaZeroModulesIdentityPresentationAuthCommandsSetMainDeviceSetMainDeviceEndpoint' does not exist on type '...'`
* **File**: `src/app/[tenant]/layout.tsx`
  * Line 23: `Property 'secondaryColor' does not exist on type 'AlphaZeroModulesTenantsPresentationEndpointsLookupTenantLookupTenantBranding'`
* **File**: `src/app/[tenant]/page.tsx`
  * Line 44: `Object literal may only specify known properties, and 'studentId' does not exist in type 'AlphaZeroModulesLibraryPresentationEndpointsRedeemCodeRedeemCodeRequest'`
  * Lines 122, 129, 131: `Property 'enrollments' does not exist on type 'AlphaZeroModulesCoursesPresentationEnrollementsDashboardDashboardResponse'`
* **File**: `src/app/[tenant]/register/page.tsx`
  * Line 25: `Property 'alphaZeroModulesIdentityPresentationAuthCommandsRegisterStudentRegisterStudentEndpoint' does not exist on type '...'`
* **File**: `src/app/page.tsx`
  * Line 15: `Object literal may only specify known properties, and 'PageNumber' does not exist in type '...'`
* **File**: `src/components/Quiz.tsx`
  * Line 23: `Property 'alphaZeroModulesCoursesPresentationCoursesCompleteItemCompleteItemEndpoint' does not exist on type '...'`

### Backend Running Status & Start Instructions
* We identified that the backend is a .NET Core modular monolith application. The root contains `AlphaZero.sln` and the Aspire AppHost: `src/aspire/AlphaZero.AppHost/AlphaZero.AppHost.csproj`.
* The `AppHost.cs` file (lines 143-167) shows that it orchestrates:
  * A Postgres/PostGIS container (`postgres`).
  * Two databases: `alphazerodb` (main app) and `idsrvDb` (Keycloak).
  * A Keycloak container (`idsrv`) using config `KeycloakConfiguration.json`.
  * The API server (`alphazero-api`) linked to S3/SQS and databases.
* The `Makefile` in the root directory contains the startup target:
  ```makefile
  run:
  	dotnet run --project src/aspire/AlphaZero.AppHost/AlphaZero.AppHost.csproj
  ```
* There are no active dotnet or container services running currently. Docker running on the host is required for startup.

### Playwright Installation & Configuration
* Inside `frontend-web/`, we verified the presence of `playwright.config.ts` and the `node_modules/@playwright` folder.
* The Playwright configuration (`playwright.config.ts` lines 14-34) specifies:
  * Test Directory: `./tests`
  * Browser: Chrome (Chromium) is enabled; Firefox and WebKit are currently commented out.
  * Base URL: Left commented out, but `TEST_INFRA.md` states it uses dynamic tenant-specific localhost subdomains (e.g. `http://damascus.localhost:3000`).
* We verified that `frontend-web/tests/example.spec.ts` exists but contains only the default Playwright demonstration tests pointing to `https://playwright.dev/`. No application-specific E2E tests are implemented.

### Database Seed & Reset Mechanisms
* **Startup Seeding**: On application startup, `Program.cs` calls `IdentitySeedReader.SeedAsync(identityContext)` (lines 74-81) when in `Development`. This reads from:
  * `src/Modules/Identity/Domain/SeedData/ManagedPolicies.json`
  * `src/Modules/Identity/Domain/SeedData/PrincipalTemplates.json`
  This seeds global policies (`StudentAccess`, `TeacherAccess`, `SuperAdminAccess`) and default principals (e.g. a `SuperAdmin` user with default credentials: username `superadmin` / password `admin`).
* **Test Seeding**: In backend integration tests (`tests/Modules/Courses/Courses.Tests.Integration/`), we observed:
  * `ApiFactory.cs` spins up a PostgreSQL container using `Testcontainers`.
  * `BaseIntegrationTest.cs` uses `Respawn` to truncate tables belonging to the `AppDbContext.Schema` namespace before each test runs.
  * Integration tests seed data programmatically by sending JSON requests to endpoints via an in-memory `HttpClient` or by querying the `DbContext` directly.
* **E2E Seeding**: There are no specialized database reset endpoints or SQL scripts in the repository intended for E2E tests.

---

## 2. Logic Chain

1. **Frontend Compilation**: The 13 TS compilation errors in 7 frontend files are due to type mismatches with the API client (`src/api/ApiClient.ts`). This implies either `ApiClient.ts` is outdated compared to the actual backend endpoints, or the frontend calls must be updated to match the current API structure.
2. **Backend Execution**: The backend uses .NET Aspire, which runs local containers for Keycloak and Postgres. To start the backend, one must execute `make run` or `dotnet run` on the AppHost project, which requires a running Docker daemon.
3. **Playwright Readiness**: Playwright is installed and configured, but the test files (`tests/example.spec.ts`) are templates and do not interact with the local application. E2E tests will need to be written from scratch.
4. **Data Reset and Seeding**: E2E testing against a live running app requires a deterministic state. While the backend has an automatic migration runner and an Identity seed (`IdentitySeedReader`), it lacks a way to wipe and re-seed business data (courses, lessons, enrollments, tenants) on demand during E2E runs. E2E tests must either:
   * Perform setup through the API starting with the seeded `SuperAdmin` account.
   * Introduce a dev-only DB reset/seed endpoint leveraging Respawn.

---

## 3. Caveats

* **Docker Access**: We were unable to inspect running Docker containers directly due to terminal permission prompts timing out. We assume Docker is not currently running the AlphaZero services based on the lack of active dotnet processes.
* **API Generation**: We did not attempt to regenerate `ApiClient.ts` to see if that resolves the type issues, as we are under a read-only constraint.
* **AWS Services**: The Aspire AppHost references AWS CDK and S3/SQS resources. Local startup may require local AWS credentials or localstack configuration depending on how the application handles AWS CDK deployments in Dev mode.

---

## 4. Conclusion

1. **Frontend**: Genuinely broken (fails to compile due to 13 type mismatches with the API client). Before E2E tests can run, the frontend compilation must be fixed.
2. **Backend**: Not running. Startable via `make run` assuming Docker and the .NET 10 SDK are present.
3. **Playwright**: Installed and configured, but lacks custom E2E tests.
4. **DB Seeding**: No pre-built E2E seed/reset endpoint exists. E2E tests should utilize API-driven setup (starting with the seeded `SuperAdmin` user) or a new Dev-only database reset endpoint.

---

## 5. Verification Method

To verify the findings:
1. Check frontend compilation:
   ```bash
   cd frontend-web
   npx tsc --noEmit
   ```
2. Check backend start commands:
   * Inspect the `Makefile` in the root directory.
   * Inspect `src/aspire/AlphaZero.AppHost/AppHost.cs`.
3. Check Playwright status:
   * Inspect `frontend-web/playwright.config.ts`.
   * Check for files inside `frontend-web/tests/`.
4. Inspect Seed files:
   * Inspect `src/Modules/Identity/Infrastructure/Persistance/Seeding/IdentitySeedReader.cs`.
   * Inspect `src/Modules/Identity/Domain/SeedData/PrincipalTemplates.json`.

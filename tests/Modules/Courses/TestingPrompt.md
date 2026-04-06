# 🪐 AlphaZero Courses Module: Senior-Level Testing Specification

## 🏗️ 1. Test Architecture & Infrastructure
You are a Senior QA Automation Engineer. Your task is to write a comprehensive integration and functional test suite for the **Courses Module**. Adhere to these architectural constraints:

- **Framework:** xUnit.
- **Assertions:** FluentAssertions.
- **Isolation:** Use `WebApplicationFactory<TEntryPoint>` (ApiFactory) with a **Base Integration Test** class.
- **Database:** Use a shared `CollectionFixture` to manage a test database (PostgreSQL via Testcontainers is preferred).
- **External Mocks:** Use `WireMock.Net` to mock any external HTTP dependencies (Identity services, etc.).
- **Data Generation:** Use `Bogus` (Faker) for all randomized yet realistic test data.
- **Tenant Context:** Every request MUST simulate the `ITenantProvider` behavior. Tests should explicitly verify that data is filtered by `TenantId`.
- **Clean State:** Ensure each test is isolated. Use a "Respawn" or "Database Reset" strategy between tests.

---

## 🧪 2. Use Case Scenarios & Flow Matrices

### A. Subject Management
*   **Create Subject:**
    *   `Success`: Valid name/description + active TenantId.
    *   `Failure`: Empty name, name > 200 chars.
    *   `Security`: Attempt to create without a valid `TenantId` (expect 401/Unauthorized).
*   **Retrieve/List Subjects:**
    *   `Success`: Retrieve specific ID.
    *   `Not Found`: Retrieve non-existent ID.
    *   `Pagination`: List subjects with `Page=1, PerPage=5` and verify count/order.
    *   `Isolation`: Verify Subject created in Tenant A is NOT visible in Tenant B’s list.

### B. Course Construction & Lifecycle
*   **Course Creation:**
    *   `Success`: Course created in `Draft` status.
    *   `Failure`: Reference a `SubjectId` that belongs to a different tenant.
*   **Curriculum Building (Sections, Lessons, Quizzes):**
    *   `Add Section`: Multiple sections added, verify ordering.
    *   `Add Lesson/Quiz`: Add items to valid/invalid sections.
    *   `Validation`: Add lesson with empty title or invalid `VideoId`.
*   **Reordering Logic:**
    *   `Sections`: Send a list of `SectionIds` in a new order; verify the `Order` property in DB.
    *   `Items`: Reorder lessons/quizzes within a section; verify atomicity.
*   **State Machine Transitions:**
    *   `Draft -> SubmittedForReview`: Success path.
    *   `SubmittedForReview -> Approved/Rejected`: Verify "Reason" is required for rejection.
    *   `Approved -> Published`: Course becomes visible to students.
    *   `Forbidden Transitions`: Attempt to `Publish` a `Draft` course directly (expect business rule error).

### C. Enrollment & Bitmask Progress Tracking
*   **Enrollment Flow:**
    *   `Success`: Create enrollment for a `Published` course.
    *   `Idempotency`: Attempt to enroll a student twice in the same course (expect 409 Conflict).
    *   `Cross-Tenant`: Verify student can enroll in courses across different tenants (Academies).
*   **Progress Tracking (The Bitmask):**
    *   `Completion`: Mark lesson at `BitIndex: 0` as complete. Verify bitmask updates.
    *   `Percentage`: After marking 2 of 4 items, verify `CompletionPercentage` is exactly `50.0`.
    *   `Validation`: Attempt to complete a `BitIndex` that exceeds the course's total items.
*   **Student Dashboard:**
    *   `Query`: Fetch dashboard for `StudentId`.
    *   `Grouping`: Verify results are correctly grouped by `TenantId` (Academy).
    *   `Filtering`: Ensure `Archived` courses do not appear in the active dashboard.

---

## 🛠️ 3. Implementation Details for the AI

### Service Mocking (Infrastructure Layer)
Refer to `src/Modules/Courses/Infrastructure/DependencyInjection.cs`. You must mock or provide implementations for:
1.  **ITenantProvider:** Use a `Mock<ITenantProvider>` or a custom `TestTenantProvider` that allows setting the current `TenantId` via request headers or test setup.
2.  **IUnitOfWork:** Ensure the `UnitOfWorkDecoratorCommandHandler` is triggered so that changes are actually committed to the test database.
3.  **Logger:** Use `ILogger<T>` to verify that critical business events (Course Approved, Enrollment Failed) are logged.

### Test Structure Example
```csharp
public class CreateCourseTests : BaseIntegrationTest
{
    public CreateCourseTests(ApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Should_CreateCourse_When_RequestIsValid()
    {
        // Arrange: Setup Tenant Context & Faker Data
        var tenantId = Guid.NewGuid();
        SetTenantHeader(tenantId);
        var subjectId = await SeedSubject(tenantId);
        var request = new CreateCourseRequest("C# Advanced", "Description", subjectId);

        // Act: POST to /courses
        var response = await Client.PostAsJsonAsync("/courses", request);

        // Assert: FluentAssertions
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var courseId = await response.Content.ReadFromJsonAsync<Guid>();
        
        // Database Verification
        var course = await ExecuteDbContextAsync(db => db.Courses.FindAsync(courseId));
        course.Status.Should().Be(CourseStatus.Draft);
        course.TenantId.Should().Be(tenantId);
    }
}
```

## 📝 4. Final Instruction
Write the tests in a **declarative, boring style**. Avoid clever hacks. Each test should clearly follow **Arrange -> Act -> Assert**. Prioritize testing the **Domain Rules** (e.g., bitmask logic) within the Integration tests to ensure the database handles the `VARBIT` types correctly. Focus heavily on **Boundary Testing** for the `Reorder` and `CompleteItem` use cases.

# 📝 Courses Module API Audit

This file details all audited endpoints, request/response models, FluentValidation constraints, and ErrorOr error codes for the Courses module.

---

## Courses & Curriculum

### 1. `POST /courses`
- **File:** `src/alphazero-api/Modules/Courses/Presentation/Courses/Create/CreateCourse.cs`
- **Endpoint:** `CreateCourseEndpoint`
- **Command:** `CreateCourseCommand(string Title, string? Description, Guid SubjectId)`
- **Request DTO:** `CreateCourseRequest { string Title, string? Description, Guid SubjectId }`
- **Response DTO:** `CreateCourseResponse(Guid Id)`
- **Success Status:** `201 Created`
- **Authorization:** `courses:Create` on `ResourceArn.ForTenant(tenantId)`
- **FluentValidation Rules (`CreateCourseCommandValidator`):**
  - `Title`: NotEmpty, MaximumLength(255)
  - `SubjectId`: NotEmpty
- **Error Codes & Status Mappings:**
  - `400 Bad Request` (`ProblemDetails`):
    - FluentValidation errors (invalid Title, empty SubjectId)
    - `Course.Title` ("Title is required.")
  - `401 Unauthorized` (`ProblemDetails`):
    - Unauthenticated request
    - `Tenant.NotFound` ("Tenant not found.")
  - `403 Forbidden` (`ProblemDetails`):
    - Missing `courses:Create` permission
  - `404 Not Found` (`ProblemDetails`):
    - `Course.SubjectId` ("Provided SubjectId does not exist.")

---

### 2. `GET /courses/{Id}`
- **File:** `src/alphazero-api/Modules/Courses/Presentation/Courses/Get/GetCourse.cs`
- **Endpoint:** `GetCourseEndpoint`
- **Query:** `GetCourseQuery(Guid Id)`
- **Request DTO:** `GetCourseRequest { Guid Id }`
- **Response DTO:** `CourseDto`
- **Success Status:** `200 OK`
- **Authorization:** `courses:View` on `ResourceArn.ForCourse(tenantId, req.Id)`
- **Error Codes & Status Mappings:**
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `courses:View` permission
  - `404 Not Found` (`ProblemDetails`):
    - `Course.NotFound` ("Course not found.")

---

### 3. `GET /courses`
- **File:** `src/alphazero-api/Modules/Courses/Presentation/Courses/List/ListCourses.cs`
- **Endpoint:** `ListCoursesEndpoint`
- **Query:** `ListCoursesQuery(int Page = 1, int PerPage = 10, Guid? SubjectId = null, string? Status = null)`
- **Request DTO:** `ListCoursesRequest { int Page, int PerPage, Guid? SubjectId, string? Status }`
- **Response DTO:** `PagedResult<CourseSummaryDto>`
- **Success Status:** `200 OK`
- **Authorization:** `courses:View` on `ResourceArn.ForTenant(tenantId)`
- **Error Codes & Status Mappings:**
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `courses:View` permission

---

### 4. `POST /courses/{CourseId}/sections`
- **File:** `src/alphazero-api/Modules/Courses/Presentation/Courses/AddSection/AddSection.cs`
- **Endpoint:** `AddSectionEndpoint`
- **Command:** `AddSectionCommand(Guid CourseId, string Title)`
- **Request DTO:** `AddSectionRequest { Guid CourseId, string Title }`
- **Response DTO:** `AddSectionResponse(Guid Id)`
- **Success Status:** `201 Created`
- **Authorization:** `courses:Edit` on `ResourceArn.ForCourse(tenantId, req.CourseId)`
- **FluentValidation Rules (`AddSectionCommandValidator`):**
  - `CourseId`: NotEmpty
  - `Title`: NotEmpty, MaximumLength(200)
- **Error Codes & Status Mappings:**
  - `400 Bad Request` (`ProblemDetails`): FluentValidation rules
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `courses:Edit` permission
  - `404 Not Found` (`ProblemDetails`):
    - `Course.NotFound` ("Course not found.")

---

### 5. `POST /courses/{CourseId}/sections/{SectionId}/lessons`
- **File:** `src/alphazero-api/Modules/Courses/Presentation/Courses/AddItem/AddLesson.cs`
- **Endpoint:** `AddLessonEndpoint`
- **Command:** `AddLessonCommand(Guid CourseId, Guid SectionId, string Title, Guid VideoId)`
- **Request DTO:** `AddLessonRequest { Guid CourseId, Guid SectionId, string Title, Guid VideoId }`
- **Response DTO:** `AddLessonResponse(Guid Id)`
- **Success Status:** `201 Created`
- **Authorization:** `courses:Edit` on `ResourceArn.ForCourse(tenantId, req.CourseId)`
- **FluentValidation Rules (`AddLessonCommandValidator`):**
  - `CourseId`, `SectionId`, `VideoId`: NotEmpty
  - `Title`: NotEmpty, MaximumLength(200)
- **Error Codes & Status Mappings:**
  - `400 Bad Request` (`ProblemDetails`): FluentValidation rules, `CurriculumItem.TenantMismatch`
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `courses:Edit` permission
  - `404 Not Found` (`ProblemDetails`):
    - `Course.NotFound` ("Course not found.")
    - `Course.Section` ("Section not found.")

---

### 6. `POST /courses/{CourseId}/sections/{SectionId}/quizzes`
- **File:** `src/alphazero-api/Modules/Courses/Presentation/Courses/AddItem/AddQuiz.cs`
- **Endpoint:** `AddQuizEndpoint`
- **Command:** `AddAssessmentCommand(Guid CourseId, Guid SectionId, string Title, Guid AssessmentId)`
- **Request DTO:** `AddQuizRequest { Guid CourseId, Guid SectionId, string Title, Guid AssessmentId }`
- **Response DTO:** `AddQuizResponse(Guid Id)`
- **Success Status:** `201 Created`
- **Authorization:** `courses:Edit` on `ResourceArn.ForCourse(tenantId, req.CourseId)`
- **FluentValidation Rules (`AddAssessmentCommandValidator`):**
  - `CourseId`, `SectionId`: NotEmpty
  - `Title`: NotEmpty, MaximumLength(200)
- **Error Codes & Status Mappings:**
  - `400 Bad Request` (`ProblemDetails`): FluentValidation rules
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `courses:Edit` permission
  - `404 Not Found` (`ProblemDetails`):
    - `Course.NotFound` ("Course not found.")
    - `Course.Section` ("Section not found.")
    - `Assessment.NotFound` ("Assessment was not found or belongs to another tenant.")

---

### 7. `POST /courses/{CourseId}/sections/reorder`
- **File:** `src/alphazero-api/Modules/Courses/Presentation/Courses/Reorder/Sections/ReorderSections.cs`
- **Endpoint:** `ReorderSectionsEndpoint`
- **Command:** `ReorderSectionsCommand(Guid CourseId, List<Guid> SectionIds)`
- **Request DTO:** `ReorderSectionsRequest { Guid CourseId, List<Guid> SectionIds }`
- **Response DTO:** None (`Send.NoContentAsync`)
- **Success Status:** `204 NoContent`
- **Authorization:** `courses:Edit` on `ResourceArn.ForCourse(tenantId, req.CourseId)`
- **Error Codes & Status Mappings:**
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `courses:Edit` permission
  - `404 Not Found` (`ProblemDetails`): `Course.NotFound`
  - `409 Conflict` (`ProblemDetails`): `Course.Status` ("Cannot reorder sections once published as it may confuse existing students.")

---

### 8. `POST /courses/{CourseId}/sections/{SectionId}/reorder`
- **File:** `src/alphazero-api/Modules/Courses/Presentation/Courses/Reorder/Items/ReorderItems.cs`
- **Endpoint:** `ReorderItemsEndpoint`
- **Command:** `ReorderItemsCommand(Guid CourseId, Guid SectionId, List<Guid> ItemIds)`
- **Request DTO:** `ReorderItemsRequest { Guid CourseId, Guid SectionId, List<Guid> ItemIds }`
- **Response DTO:** None (`Send.NoContentAsync`)
- **Success Status:** `204 NoContent`
- **Authorization:** `courses:Edit` on `ResourceArn.ForCourse(tenantId, req.CourseId)`
- **Error Codes & Status Mappings:**
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `courses:Edit` permission
  - `404 Not Found` (`ProblemDetails`): `Course.NotFound`, `Course.Section`

---

## Course Lifecycle / State

### 9. `PATCH /courses/{CourseId}/review`
- **File:** `src/alphazero-api/Modules/Courses/Presentation/Courses/State/SubmitForReview.cs`
- **Endpoint:** `SubmitForReviewEndpoint`
- **Command:** `SubmitCourseForReviewCommand(Guid CourseId)`
- **Request DTO:** `SubmitForReviewRequest { Guid CourseId }`
- **Response DTO:** None (`Send.NoContentAsync`)
- **Success Status:** `204 NoContent`
- **Authorization:** `courses:Submit` on `ResourceArn.ForCourse(tenantId, req.CourseId)`
- **Error Codes & Status Mappings:**
  - `400 Bad Request` (`ProblemDetails`): `Course.Empty` ("Course must have content before review.")
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `courses:Submit` permission
  - `404 Not Found` (`ProblemDetails`): `Course.NotFound`
  - `409 Conflict` (`ProblemDetails`): `Course.Status` ("Only draft courses can be reviewed.")

---

### 10. `PATCH /courses/{CourseId}/approve`
- **File:** `src/alphazero-api/Modules/Courses/Presentation/Courses/State/Approve.cs`
- **Endpoint:** `ApproveEndpoint`
- **Command:** `ApproveCourseCommand(Guid CourseId)`
- **Request DTO:** `ApproveRequest { Guid CourseId }`
- **Response DTO:** None (`Send.NoContentAsync`)
- **Success Status:** `204 NoContent`
- **Authorization:** `courses:Approve` on `ResourceArn.ForCourse(tenantId, req.CourseId)`
- **Error Codes & Status Mappings:**
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `courses:Approve` permission
  - `404 Not Found` (`ProblemDetails`): `Course.NotFound`
  - `409 Conflict` (`ProblemDetails`): `Course.Status` ("Only courses under review can be approved.")

---

### 11. `PATCH /courses/{CourseId}/reject`
- **File:** `src/alphazero-api/Modules/Courses/Presentation/Courses/State/Reject.cs`
- **Endpoint:** `RejectEndpoint`
- **Command:** `RejectCourseCommand(Guid CourseId, string Reason)`
- **Request DTO:** `RejectRequest { Guid CourseId, string Reason }`
- **Response DTO:** None (`Send.NoContentAsync`)
- **Success Status:** `204 NoContent`
- **Authorization:** `courses:Reject` on `ResourceArn.ForCourse(tenantId, req.CourseId)`
- **Error Codes & Status Mappings:**
  - `400 Bad Request` (`ProblemDetails`): `Course.RejectionReason` ("Rejection reason is required.")
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `courses:Reject` permission
  - `404 Not Found` (`ProblemDetails`): `Course.NotFound`
  - `409 Conflict` (`ProblemDetails`): `Course.Status` ("Only courses under review can be rejected.")

---

### 12. `PATCH /courses/{CourseId}/publish`
- **File:** `src/alphazero-api/Modules/Courses/Presentation/Courses/State/Publish.cs`
- **Endpoint:** `PublishEndpoint`
- **Command:** `PublishCourseCommand(Guid CourseId)`
- **Request DTO:** `PublishRequest { Guid CourseId }`
- **Response DTO:** None (`Send.NoContentAsync`)
- **Success Status:** `204 NoContent`
- **Authorization:** `courses:Publish` on `ResourceArn.ForCourse(tenantId, req.CourseId)`
- **Error Codes & Status Mappings:**
  - `400 Bad Request` (`ProblemDetails`): `Course.NoPlans` ("Course must have at least one plan before it can be published.")
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `courses:Publish` permission
  - `404 Not Found` (`ProblemDetails`): `Course.NotFound`
  - `409 Conflict` (`ProblemDetails`): `Course.Status` ("Only approved courses can be published.")

---

## Plans

### 13. `POST /courses/{CourseId}/plans`
- **File:** `src/alphazero-api/Modules/Courses/Presentation/Courses/Plans/AddPlan/AddPlan.cs`
- **Endpoint:** `AddPlanEndpoint`
- **Command:** `AddPlanCommand(Guid CourseId, string Name, string? Description, decimal Price, int DurationDays, Guid PrincipalId)`
- **Request DTO:** `AddPlanRequest { Guid CourseId, string Name, string? Description, decimal Price, int DurationDays, Guid PrincipalId }`
- **Response DTO:** `AddPlanResponse(Guid Id)`
- **Success Status:** `201 Created`
- **Authorization:** `courses:Edit` on `ResourceArn.ForCourse(tenantId, req.CourseId)`
- **FluentValidation Rules (`AddPlanCommandValidator`):**
  - `CourseId`, `PrincipalId`: NotEmpty
  - `Name`: NotEmpty, MaximumLength(128)
- **Error Codes & Status Mappings:**
  - `400 Bad Request` (`ProblemDetails`): FluentValidation rules, `Course.PlanName` ("Plan name is required.")
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `courses:Edit` permission
  - `404 Not Found` (`ProblemDetails`): `Course.NotFound`

---

### 14. `PUT /courses/{CourseId}/plans/{PlanId}`
- **File:** `src/alphazero-api/Modules/Courses/Presentation/Courses/Plans/UpdatePlan/UpdatePlan.cs`
- **Endpoint:** `UpdatePlanEndpoint`
- **Command:** `UpdatePlanCommand(Guid CourseId, Guid PlanId, string Name, string? Description, decimal Price, int DurationDays, Guid PrincipalId)`
- **Request DTO:** `UpdatePlanRequest { Guid CourseId, Guid PlanId, string Name, string? Description, decimal Price, int DurationDays, Guid PrincipalId }`
- **Response DTO:** None (`Send.NoContentAsync`)
- **Success Status:** `204 NoContent`
- **Authorization:** `courses:Edit` on `ResourceArn.ForCourse(tenantId, req.CourseId)`
- **FluentValidation Rules (`UpdatePlanCommandValidator`):**
  - `CourseId`, `PlanId`, `PrincipalId`: NotEmpty
  - `Name`: NotEmpty, MaximumLength(128)
- **Error Codes & Status Mappings:**
  - `400 Bad Request` (`ProblemDetails`): FluentValidation rules, `Course.PlanName`
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `courses:Edit` permission
  - `404 Not Found` (`ProblemDetails`): `Course.NotFound`, `Course.Plan` ("Plan not found.")

---

### 15. `DELETE /courses/{CourseId}/plans/{PlanId}`
- **File:** `src/alphazero-api/Modules/Courses/Presentation/Courses/Plans/RemovePlan/RemovePlan.cs`
- **Endpoint:** `RemovePlanEndpoint`
- **Command:** `RemovePlanCommand(Guid CourseId, Guid PlanId)`
- **Request DTO:** `RemovePlanRequest { Guid CourseId, Guid PlanId }`
- **Response DTO:** None (`Send.NoContentAsync`)
- **Success Status:** `204 NoContent`
- **Authorization:** `courses:Edit` on `ResourceArn.ForCourse(tenantId, req.CourseId)`
- **FluentValidation Rules (`RemovePlanCommandValidator`):**
  - `CourseId`, `PlanId`: NotEmpty
- **Error Codes & Status Mappings:**
  - `400 Bad Request` (`ProblemDetails`): FluentValidation rules
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `courses:Edit` permission
  - `404 Not Found` (`ProblemDetails`): `Course.NotFound`, `Course.Plan` ("Plan not found.")

---

## Enrollments & Progress

### 16. `POST /courses/enroll`
- **File:** `src/alphazero-api/Modules/Courses/Presentation/Enrollements/Enroll/EnrollInCourse.cs`
- **Endpoint:** `EnrollInCourseEndpoint`
- **Command:** `EnrollInCourseCommand(Guid StudentId, Guid CourseId)`
- **Request DTO:** `EnrollInCourseRequest { Guid StudentId, Guid CourseId }`
- **Response DTO:** `EnrollInCourseResponse(Guid Id)`
- **Success Status:** `201 Created`
- **Authorization:** `courses:Enroll` on `ResourceArn.ForCourse(tenantId, req.CourseId)`
- **FluentValidation Rules (`EnrollInCourseCommandValidator`):**
  - `StudentId`, `CourseId`: NotEmpty
- **Error Codes & Status Mappings:**
  - `400 Bad Request` (`ProblemDetails`): FluentValidation rules, `Enrollement.StudentId`, `Enrollement.CourseId`
  - `401 Unauthorized` (`ProblemDetails`):
    - Unauthenticated request
    - `Tenant.NotFound` ("Tenant not found.")
  - `403 Forbidden` (`ProblemDetails`): Missing `courses:Enroll` permission
  - `404 Not Found` (`ProblemDetails`): `Course.NotFound`
  - `409 Conflict` (`ProblemDetails`): `Enrollment.Exists` ("Student is already enrolled in this course.")

---

### 17. `GET /courses/enrollments/{Id}`
- **File:** `src/alphazero-api/Modules/Courses/Presentation/Enrollements/Get/GetEnrollement.cs`
- **Endpoint:** `GetEnrollementEndpoint`
- **Query:** `GetEnrollementQuery(Guid Id)`
- **Request DTO:** `GetEnrollementRequest { Guid Id }`
- **Response DTO:** `EnrollementDto`
- **Success Status:** `200 OK`
- **Authorization:** `enrollments:View` on `ResourceArn.ForEnrollment(tenantId, req.Id)`
- **Error Codes & Status Mappings:**
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `enrollments:View` permission
  - `404 Not Found` (`ProblemDetails`): `Enrollment.NotFound` ("Enrollment not found.")

---

### 18. `POST /courses/enrollements/{EnrollmentId}/complete`
- **File:** `src/alphazero-api/Modules/Courses/Presentation/Enrollements/CompleteItem/CompleteItem.cs`
- **Endpoint:** `CompleteItemEndpoint`
- **Command:** `CompleteItemCommand(Guid EnrollmentId, Guid ItemId)`
- **Request DTO:** `CompleteItemRequest { Guid EnrollmentId, Guid ItemId }`
- **Response DTO:** None (`Send.NoContentAsync`)
- **Success Status:** `204 NoContent`
- **Authorization:** `enrollments:Complete` on `ResourceArn.ForEnrollment(tenantId, req.EnrollmentId)`
- **Error Codes & Status Mappings:**
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `enrollments:Complete` permission
  - `404 Not Found` (`ProblemDetails`): `Enrollment.NotFound` ("Enrollment not found."), `Course.Item` ("Item not found in this course.")
  - `409 Conflict` (`ProblemDetails`): `Enrollement.Status` ("Cannot complete items in an inactive enrollment.")

---

### 19. `GET /courses/dashboard/{StudentId}`
- **File:** `src/alphazero-api/Modules/Courses/Presentation/Enrollements/Dashboard/GetStudentDashboard.cs`
- **Endpoint:** `GetStudentDashboardEndpoint`
- **Query:** `GetStudentDashboardQuery(Guid StudentId)`
- **Request DTO:** `GetStudentDashboardRequest { Guid StudentId }`
- **Response DTO:** `StudentDashboardDto`
- **Success Status:** `200 OK`
- **Authorization:** `enrollments:View` on `ResourceArn.ForTenant(tenantId)`
- **Error Codes & Status Mappings:**
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `enrollments:View` permission

---

## Subjects

### 20. `POST /courses/subjects`
- **File:** `src/alphazero-api/Modules/Courses/Presentation/Subjects/Create/CreateSubject.cs`
- **Endpoint:** `CreateSubjectEndpoint`
- **Command:** `CreateSubjectCommand(string Name, string? Description)`
- **Request DTO:** `CreateSubjectRequest { string Name, string? Description }`
- **Response DTO:** `CreateSubjectResponse(Guid Id)`
- **Success Status:** `201 Created`
- **Authorization:** `subjects:Create` on `ResourceArn.ForTenant(tenantId)`
- **FluentValidation Rules (`CreateSubjectCommandValidator`):**
  - `Name`: NotEmpty, MaximumLength(200)
  - `Description`: MaximumLength(1000)
- **Error Codes & Status Mappings:**
  - `400 Bad Request` (`ProblemDetails`): FluentValidation rules, `Subject.Validation` ("no name is provided")
  - `401 Unauthorized` (`ProblemDetails`):
    - Unauthenticated request
    - `Tenant.NotFound` ("Tenant could not be determined.")
  - `403 Forbidden` (`ProblemDetails`): Missing `subjects:Create` permission

---

### 21. `GET /courses/subjects/{id}`
- **File:** `src/alphazero-api/Modules/Courses/Presentation/Subjects/Get/GetSubject.cs`
- **Endpoint:** `GetSubjectEndpoint`
- **Query:** `GetSubjectQuery(Guid Id)`
- **Request DTO:** `GetSubjectRequest { Guid Id }`
- **Response DTO:** `SubjectDto`
- **Success Status:** `200 OK`
- **Authorization:** `subjects:View` on `ResourceArn.ForTenant(tenantId)`
- **Error Codes & Status Mappings:**
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `subjects:View` permission
  - `404 Not Found` (`ProblemDetails`): Subject not found

---

### 22. `GET /courses/subjects`
- **File:** `src/alphazero-api/Modules/Courses/Presentation/Subjects/List/ListSubjects.cs`
- **Endpoint:** `ListSubjectsEndpoint`
- **Query:** `ListSubjectsQuery(int Page = 1, int PerPage = 10)`
- **Request DTO:** `ListSubjectsRequest { int Page, int PerPage }`
- **Response DTO:** `PagedResult<SubjectDto>`
- **Success Status:** `200 OK`
- **Authorization:** `subjects:List` on `ResourceArn.ForTenant(tenantId)`
- **Error Codes & Status Mappings:**
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `subjects:List` permission

---

## Analytics

### 23. `GET /courses/{CourseId}/analytics`
- **File:** `src/alphazero-api/Modules/Courses/Presentation/Analytics/GetCourseAnalytics.cs`
- **Endpoint:** `GetCourseAnalyticsEndpoint`
- **Query:** `GetCourseAnalyticsQuery(Guid CourseId)`
- **Request DTO:** `GetCourseAnalyticsRequest { Guid CourseId }`
- **Response DTO:** `CourseAnalyticsDto`
- **Success Status:** `200 OK`
- **Authorization:** `courses:ViewAnalytics` on `ResourceArn.ForCourse(tenantId, req.CourseId)`
- **Error Codes & Status Mappings:**
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `courses:ViewAnalytics` permission
  - `404 Not Found` (`ProblemDetails`): `CourseAnalytics.NotFound` ("Analytics not found for this course.")

---

### 24. `GET /courses/{CourseId}/students`
- **File:** `src/alphazero-api/Modules/Courses/Presentation/Analytics/ListStudentProgress.cs`
- **Endpoint:** `ListStudentProgressEndpoint`
- **Query:** `ListStudentProgressQuery(Guid CourseId, int Page = 1, int PerPage = 10)`
- **Request DTO:** `ListStudentProgressRequest { Guid CourseId, int Page, int PerPage }`
- **Response DTO:** `PagedResult<StudentProgressDto>`
- **Success Status:** `200 OK`
- **Authorization:** `courses:ViewAnalytics` on `ResourceArn.ForCourse(tenantId, req.CourseId)`
- **Error Codes & Status Mappings:**
  - `401 Unauthorized` (`ProblemDetails`): Unauthenticated request
  - `403 Forbidden` (`ProblemDetails`): Missing `courses:ViewAnalytics` permission

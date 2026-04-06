# 🪐 AlphaZero Courses Module: Test Scenarios & Flow Matrix

This document outlines the comprehensive test suite for the Courses Module, covering integration, functional, and boundary scenarios.

## 🏗️ 1. Subject Management

| Scenario | Type | Expected Result |
| :--- | :--- | :--- |
| **Create Subject (Valid)** | Success | 200 OK, returns ID, persisted in DB with correct `TenantId`. |
| **Create Subject (Empty Name)** | Failure | 400 Bad Request / Validation Error. |
| **Create Subject (Unauthorized)** | Security | 401 Unauthorized if `TenantId` is missing from context. |
| **Get Subject (Existing)** | Success | 200 OK, returns correct Subject details. |
| **Get Subject (Non-Existent)** | Failure | 404 Not Found. |
| **List Subjects (Pagination)** | Success | Returns `PagedResult`, respects `Page` and `PerPage`. |
| **Subject Isolation** | Isolation | Tenant A cannot see or retrieve subjects created by Tenant B. |

## 🏗️ 2. Course Construction & Lifecycle

| Scenario | Type | Expected Result |
| :--- | :--- | :--- |
| **Create Course (Valid)** | Success | 201 Created, Status: `Draft`. |
| **Create Course (Cross-Tenant)** | Failure | 404/400 if `SubjectId` belongs to a different tenant. |
| **Add Section** | Success | Section added with incremented `Order` property. |
| **Add Lesson (Valid)** | Success | Lesson added to section, `BitIndex` assigned sequentially. |
| **Add Lesson (Invalid Video)** | Failure | 400 Bad Request if `VideoId` is empty or invalid. |
| **Reorder Sections** | Success | Atomic update of section orders; verified via GET. |
| **Reorder Items** | Success | Lessons/Quizzes reordered within a section; orders updated in DB. |
| **State: Draft -> Review** | Lifecycle | Success if course is not empty. |
| **State: Review -> Approve** | Lifecycle | Transitions status to `Approved`. |
| **State: Review -> Reject** | Lifecycle | Requires `Reason`; transitions back to `Draft`. |
| **State: Approved -> Publish** | Lifecycle | Transitions to `Published`; raises `CoursePublishedDomainEvent`. |
| **Forbidden Transition** | Business | Attempting to `Publish` directly from `Draft` returns error. |

## 🏗️ 3. Enrollment & Progress (Bitmask Logic)

| Scenario | Type | Expected Result |
| :--- | :--- | :--- |
| **Enroll Student** | Success | 201 Created, Progress initialized to 0. |
| **Double Enrollment** | Conflict | 409 Conflict if student is already enrolled. |
| **Enroll in Draft Course** | Failure | 400/Business Rule error (cannot enroll in non-published courses). |
| **Complete Item (Bit 0)** | Success | Bitmask updated, `CompletionPercentage` recalculated. |
| **Complete Item (Out of Range)** | Failure | 400 Bad Request if `BitIndex` exceeds total items in course. |
| **Progress Accuracy** | Logic | Marking 1 of 2 items complete results in exactly `50.0%`. |
| **Cross-Tenant Enrollment** | Multi-tenant | Student can enroll in courses across different Academies. |

## 🏗️ 4. Student Dashboard

| Scenario | Type | Expected Result |
| :--- | :--- | :--- |
| **Fetch Dashboard** | Success | Enrollments grouped by `TenantId` (Academy). |
| **Dashboard Isolation** | Privacy | Dashboard only shows enrollments for the requesting `StudentId`. |
| **Filtering Archived** | Logic | Courses in `Archived` status do not appear in the active dashboard. |

---

## 🛠️ Implementation Notes for Developers
- **Database:** Ensure the PostgreSQL `VARBIT` type is correctly mapped in `AppDbContext`.
- **Tenant Context:** Use the `TestTenantProvider` in integration tests to simulate switching between Academies.
- **Cleanup:** All integration tests should inherit from `BaseIntegrationTest` to ensure the database is reset between runs.

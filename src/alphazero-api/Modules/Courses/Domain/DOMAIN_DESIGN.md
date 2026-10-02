# 🪐 Courses Module - Domain Strategic Design

This document outlines the Domain-Driven Design (DDD) structure for the `Courses` bounded context. This module is built on **Enterprise Clean Architecture** and strictly follows the **Modular Monolith** pattern.

---

## 🏗️ Aggregates & Entities

### 1. `Course` (Aggregate Root)
The primary unit of content delivery. It manages the structure and lifecycle of educational material.
- **Entities:**
    - `CourseSection`: A logical grouping of items (e.g., "Chapter 1").
    - `CourseSectionItem`: Abstract base for `Lesson`, `Quiz`, and `Document`.
- **Key Properties:**
    - `Status`: Manages the QA workflow (`Draft`, `UnderReview`, `Approved`, `Published`).
    - `NextAvailableBitIndex`: A monotonic counter ensuring every item gets a permanent bit in the progress bitmask.
- **Lifecycle & Domain Logic:**
    - **Drafting:** Content workers can add/remove sections and items. Adding an item assigns a permanent `BitIndex`.
    - **UI Reordering:** Changing the `Order` property is allowed at any time and does not affect the `BitIndex`.
    - **Bit-Index Stability:** Once a course is `Published`, the `BitIndex` of existing items must **NEVER** change. New items can be added (taking the next available bit), but re-indexing is forbidden.
    - **QA Guardrails:** A course cannot enter `UnderReview` if it has no sections or items.

### 2. `Subject` (Aggregate Root)
Represents the educational category (e.g., Physics, Chemistry).
- **Lifecycle:**
    - **Creation:** Tenant Admins create subjects localized to their curriculum.
    - **Deactivation:** Subjects can be deactivated (soft-deleted) to prevent new courses from using them while preserving existing links.

### 3. `Enrollment` (Aggregate Root)
Tracks the relationship between a `Student` and a `Course`.
- **Lifecycle:**
    - **Creation:** Triggered by payment or code redemption. The bitmask is initialized with a length equal to the `Course.TotalTrackedItems`.
    - **Progress Tracking:** When a student finishes a video or passes a quiz, the bit at the item's specific `BitIndex` is flipped to `1`.
    - **Completion:** A course is "Complete" when all tracked bits are `1`.

---

## 🛠️ Application Layer Use Cases

### 🧑‍💻 Content Production (Workers & Teachers)
- `CreateCourseCommand`: Initialize a new course in `Draft`.
- `AddSectionCommand` / `AddLessonCommand`: Build the course structure.
- `ReorderCourseStructureCommand`: Update UI `Order` properties.
- `SubmitCourseForReviewCommand`: Signal that the teacher/manager needs to audit the content.
- `ApproveCourseCommand`: Teacher/Manager validation.
- `PublishCourseCommand`: Make the course visible to students and lock bit-indices.

### 🧑‍🎓 Learning (Students)
- `EnrollInCourseCommand`: Create a student enrollment and initialize the bitmask.
- `CompleteLessonCommand`: Flips a bit in the `Enrollment` bitmask based on the lesson's `BitIndex`.
- `GetCourseProgressQuery`: Calculates the percentage of completed bits.
- `GetStudentDashboardQuery`: Returns a list of active enrollments and their bitmask-derived progress.

### 🏛️ Management (Admins)
- `CreateSubjectCommand`: Define a new educational category.
- `DeactivateEnrollmentCommand`: Manually revoke a student's access.

---

## 💎 Value Objects

### 1. `ProgressBitmask`
- **Logic:** Encapsulates bitwise operations (`SetBit`, `IsBitSet`, `GetCompletionPercentage`).
- **Invariant:** Must be initialized with the length provided by the `Course` at the moment of enrollment.

### 2. `CourseStatus`
- **Logic:** State machine ensuring valid transitions: `Draft` -> `UnderReview` -> `Approved` -> `Published`.

---

## 📢 Domain Events

| Event | Purpose |
| :--- | :--- |
| `CoursePublishedDomainEvent` | Signals that the course is live. |
| `ItemCompletedDomainEvent` | Fired when a student completes a lesson/quiz to update progress. |
| `CourseCompletedDomainEvent` | Fired when the last bit in a bitmask is flipped to `1`. |
| `EnrollmentCreatedDomainEvent` | Triggers initial bitmask allocation. |

# 🪐 Courses Module - Domain Strategic Design

This document outlines the Domain-Driven Design (DDD) structure for the `Courses` bounded context. This module is built on **Enterprise Clean Architecture** and strictly follows the **Modular Monolith** pattern.

---

## 🏗️ Aggregates & Entities

### 1. `Course` (Aggregate Root)
The primary unit of content delivery. It manages the structure and lifecycle of educational material.
- **Entities:**
    - `Section`: A logical grouping of lessons (e.g., "Chapter 1: Mechanics").
    - `Lesson`: The atomic unit of learning (Video, PDF, or Quiz).
- **Key Properties:**
    - `TenantId`: Ensures strict multi-tenant isolation.
    - `Status`: Manages the QA workflow (`Draft`, `UnderReview`, `Approved`, `Published`).
    - `SubjectId`: Link to the localized subject.
    - `Price`: Encapsulated price (Value Object).
- **Invariants:**
    - A course cannot be `Published` unless its status is `Approved`.
    - Lesson order must be sequential and unique within the course for bitmask tracking.
    - A `Lesson` referencing a `VideoId` cannot be deleted if the video is currently in a "Live" state in the streaming module (enforced via cross-module integration events).

### 2. `Subject` (Aggregate Root)
Represents the educational category (e.g., Physics, Chemistry, Arabic).
- **Responsibility:** Managed independently by each Tenant Admin to allow localized curriculum naming.
- **Key Properties:**
    - `TenantId`: Isolated per academy.
    - `IsActive`: Soft-delete mechanism to prevent breaking existing courses.
- **Note:** While we provide "static recommendations," every tenant defines their own subject mapping.

### 3. `Enrollment` (Aggregate Root)
Tracks the relationship between a `Student` and a `Course`.
- **Responsibility:** High-volume aggregate designed for fast progress lookups and updates.
- **Key Properties:**
    - `StudentId` & `CourseId`.
    - `Progress`: A **Value Object** containing the `Bitmask` (VARBIT) logic.
    - `Status`: (`Active`, `Expired`, `Suspended`).
- **Invariants:**
    - `ProgressBitmask` length MUST exactly match the `Course.LessonCount`.
    - Progress can only be incremented for `Active` enrollments.
    - One active enrollment per course per student.

---

## 💎 Value Objects

### 1. `LessonProgress` (Bitmask)
- **Logic:** Uses bitwise operations to track completion of individual lessons.
- **Storage:** Persisted as `VARBIT` in PostgreSQL to minimize footprint.
- **Methods:** `IsCompleted(index)`, `MarkAsComplete(index)`, `CalculatePercentage()`.

### 2. `CourseStatus`
- **Logic:** A state machine ensuring a valid QA transition:
    - `Draft` -> `UnderReview` (Worker -> Teacher)
    - `UnderReview` -> `Approved` (Teacher -> Manager)
    - `Approved` -> `Published` (Manager -> System/Admin)
    - `Published` -> `Archived`.

---

## 📢 Domain Events

| Event | Purpose |
| :--- | :--- |
| `CourseApprovedEvent` | Signals that content is ready for student consumption. |
| `LessonCompletedEvent` | Triggers bitmask updates and checks for overall Course completion. |
| `EnrollmentCreatedEvent` | Triggers initial bitmask allocation (all zeros). |
| `CourseStatusChangedEvent` | Used for audit logs and notifying the content production team. |

---

## 🛡️ Cross-Module Integrity (Video Integration)
- **Constraint:** A video in the `VideoStreaming` context cannot be deleted if it is linked to a `Lesson`.
- **Implementation:** The `Courses` module will handle `VideoDeletionRequested` integration events and reply with a "Veto" if the `VideoId` is actively used in a `Published` course.

---

## 🚀 Folder Structure Recommendation
```text
📂 Domain/
├── 📂 Aggregates/
│   ├── 📂 Course/
│   │   ├── Course.cs (AR)
│   │   ├── Section.cs (Entity)
│   │   └── Lesson.cs (Entity)
│   ├── 📂 Subject/
│   │   └── Subject.cs (AR)
│   └── 📂 Enrollment/
│       └── Enrollment.cs (AR)
├── 📂 ValueObjects/
│   ├── LessonProgress.cs
│   ├── CourseStatus.cs
│   └── Money.cs
├── 📂 Events/
│   ├── CourseApprovedEvent.cs
│   └── LessonCompletedEvent.cs
└── 📂 Exceptions/
    └── DomainException.cs
```

# System Roles & Managed Policies

AlphaZero comes with several pre-defined **Principal Templates** (Roles) and **Managed Policies** to handle common authorization scenarios.

## Core System Roles

These roles are available in every Academy and use fixed GUIDs to allow for reliable automation (e.g., student assignment during enrollment).

| Role Name | GUID | Purpose |
| :--- | :--- | :--- |
| **Administrator** | `...100000000001` | Full access to all Academy features and settings. |
| **Student** | `...100000000002` | Standard access for learners (streaming, assessments). |
| **CourseWorker** | `...100000000003` | Content creation and basic management. |
| **Teacher** | `...100000000004` | Instruction, grading, and student management. |
| **LibraryManager**| `...100000000006` | Managing physical library connections and assets. |

---

## Default Managed Policies

Managed policies define the specific permissions associated with roles.

### `AdministratorAccess`
- **Actions**: `*`
- **Resources**: `*`
- **Description**: Grants full administrative rights.

### `StudentAccess`
- **Actions**: 
    - `video:Stream` (Condition: `MainDeviceOnly`)
    - `courses:View`
    - `assessments:Submit`
    - `enrollments:View`
- **Resources**: Scoped to the student's assigned courses and enrollments.
- **Description**: The base set of permissions for a learner.

### `LibraryAccountantAccess`
- **Actions**:
    - `library:SellCodes`
    - `library:Audit`
- **Resources**: `az:library:*`
- **Description**: Optimized for staff managing physical code distribution.

---

## Policy Action Registry

When defining custom policies, use these standard action strings:

| Service | Actions |
| :--- | :--- |
| **Identity** | `identity:ManagePrincipals`, `identity:ManagePolicies`, `identity:ViewProfile` |
| **Courses** | `courses:Create`, `courses:Edit`, `courses:View`, `courses:Enroll`, `courses:Publish` |
| **Video** | `video:Stream`, `video:Upload`, `video:Delete`, `video:Edit` |
| **Tenants** | `tenants:Manage`, `tenants:View` |
| **Assessments**| `assessments:Create`, `assessments:Edit`, `assessments:Submit`, `assessments:ViewSubmissions` |
| **Library** | `library:Audit`, `library:GenerateCodes`, `library:SellCodes`, `library:AttachCourses` |

## Resource Type Mapping

The `ResourceType` enum in `AlphaZero.Shared.Authorization` maps `service` segments in ARNs to module boundaries.

- `identity`
- `courses`
- `video`
- `tenants`
- `library`
- `assessments`

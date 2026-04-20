# 🔑 AlphaZero Permission Reference

This document defines the standard permission strings used across the AlphaZero platform. 

## Standard Format
`{service}:{action}` (e.g., `courses:Create`)

---

## 📂 Courses Module
| Permission | Description |
| :--- | :--- |
| `courses:Create` | Initialize a new course in Draft status. |
| `courses:View` | View course structure and content. |
| `courses:Edit` | Add/Reorder sections, lessons, and quizzes. |
| `courses:Submit` | Submit a course for QA review. |
| `courses:Approve` | Perform final QA and mark as Approved. |
| `courses:Reject` | Send course back to worker with feedback. |
| `courses:Publish` | Make course visible to all students in the tenant. |
| `courses:Enroll` | Join a course via code or payment. |
  
## 🎥 Video Module
| Permission | Description |
| :--- | :--- |
| `video:Upload` | Upload raw MP4s to S3. |
| `video:Stream` | Access HLS/Dash streaming segments. |
| `video:Edit` | Update video title or metadata. |
| `video:Delete` | Permanently remove video assets. |

## 📑 Subjects Module
| Permission | Description |
| :--- | :--- |
| `subjects:Create` | Create new grade/subject categories. |
| `subjects:List` | List all subjects in the tenant. |
| `subjects:View` | View specific subject details. |

## 🛡️ Identity Module
| Permission | Description |
| :--- | :--- |
| `identity:ManagePolicies` | Create/Delete Managed Policies. |
| `identity:ManagePrincipals` | Create IAM Principals and attach policies. |
| `identity:AssignRoles` | Assign roles to TenantUsers. |

## 🎟️ Library Module
| Permission | Description |
| :--- | :--- |
| `library:GenerateCodes` | Mint access codes for courses. |
| `library:SellCodes` | Retrieve/Invalidate codes for physical sale. |
| `library:Audit` | View financial and inventory logs. |
| `library:AttachCourses` | Map courses to specific libraries. |

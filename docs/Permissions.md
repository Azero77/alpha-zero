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
| `courses:Edit` | Add/Reorder sections, lessons, and quizzes; rename/archive/soft-delete courses and sections; deactivate enrollments. |
| `courses:Submit` | Submit a course for QA review. |
| `courses:Approve` | Perform final QA and mark as Approved. |
| `courses:Reject` | Send course back to worker with feedback. |
| `courses:Publish` | Make course visible to all students in the tenant. |
| `courses:Enroll` | Join a course via code or payment. |
| `enrollments:List` | List a student's enrollments (all statuses) for support/admin tooling. |
  
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
| `subjects:Edit` | Rename and soft-delete subject categories (refused while referenced by a course). |

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

## 💳 Payments Module
| Permission | Scope | Description |
| :--- | :--- | :--- |
| `payments:ManageCatalog` | Academy | Create or archive immutable Prices for Products owned by the actor's tenant. |
| `payments:ManageAcademySettings` | Platform | Enable online selling and set Commission Share/refund-window policy for an Academy. |
| `payments:ManageProviders` | Platform | Configure Provider Accounts and currency routes; never exposes secret values. |
| `payments:ViewAnalytics` | Academy | View one tenant's provider-account/currency analytics and transaction detail. |
| `payments:ViewPlatformAnalytics` | Platform | View all Academies while preserving provider-account/currency partitions. |
| `payments:IssueRefund` | Platform | Decide Refund Requests and issue provider-supported partial/full Refunds. |
| `payments:OpenReview` | Academy / Platform | Open an evidence-backed Payment Review Case; does not approve or move money. |
| `payments:ApproveReview` | Platform | Approve/reject a Payment Review Case; the actor must differ from its maker. |
| `payments:RecordSettlement` | Platform | Resolve an approved Settlement case after confirmed transfer evidence. |
| `payments:AdjustLedger` | Platform | Resolve an approved manual-adjustment case through the hardened Ledger posting path. |
| `payments:RunReconciliation` | Platform | Run/re-drive reconciliation and inspect persisted findings. |

Students create Checkout Sessions, view Purchases, and request supported Refunds through ownership and tenant checks rather than staff permissions. Permission checks never replace tenant, Provider Account, currency, environment, capability, or maker/checker validation in Payments commands.

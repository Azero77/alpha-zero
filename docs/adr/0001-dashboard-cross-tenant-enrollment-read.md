# Student-support dashboard may bypass query filters on the Enrollments set

**Status:** accepted (2026-11-02)

## Context

The student-support dashboard (`GetStudentDashboardQuery`) needs one student's active
enrollments across all tenants — it is the only read path in the system with that
requirement, and the handler contains the spread by grouping rows into per-tenant lists
via `TenantId`. `EnrollmentQueryService.ListStudentDashboardEnrollmentsAsync` implements
it with a query-wide `IgnoreQueryFilters()` on the Enrollments set, which the Query
Refactoring Plan's DX checklist otherwise bans for query services ("Banned
`IgnoreQueryFilters` in Query Services to protect multi-tenant boundaries").

The behavior predates the query-service refactor (the old
`GetStudentEnrollmentsAcrossTenantsAsync` did the same) and was carried over for parity.
Narrowing the bypass to named filters (Tenant + SoftDelete only) would be a behavior
change, not a fix: soft-deleted-but-Active enrollments would stop surfacing, and it
silently redefines what the dashboard shows today.

## Decision

That one read path keeps the query-wide bypass. It is the only place a query service may
drop the Enrollments tenant filter wholesale. Every other query in
`EnrollmentQueryService` — and every other query service — uses the named-filter
`IgnoreQueryFilters(string[])` overload or no bypass at all. The shapes other developers
should copy are the `SoftDeleteOnly` / `AcrossTenantsMaskReads` correlated subqueries,
not the dashboard's enrollment-set relax.

## Consequences

- Soft-deleted-but-Active enrollments can surface on the dashboard; the handler's
  `TenantId` grouping is the defense, since no row leaves its tenant group.
- Changing the dashboard to hide soft-deleted rows is an intentional behavior change to
  be reviewed as such, not a conformance bug fix.
- The DX checklist's `IgnoreQueryFilters` ban holds except for this documented exception;
  do not follow the dashboard's shape elsewhere.

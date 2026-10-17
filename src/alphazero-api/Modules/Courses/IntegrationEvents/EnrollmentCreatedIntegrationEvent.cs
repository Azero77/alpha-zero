using MassTransit;

namespace AlphaZero.Modules.Courses.IntegrationEvents;

// Wire-identity pin (#31): the type was renamed from the misspelled 'EnrollementCreatedIntegrationEvent'.
// MassTransit derives message URNs from type names, so the legacy URN is pinned here to keep
// pending EF-outbox envelopes (which embed the old URN) deliverable across the deploy.
// Scheduled for removal in a later release cycle, once the outbox is provably drained.
[MessageUrn("AlphaZero.Modules.Courses.IntegrationEvents:EnrollementCreatedIntegrationEvent")]
public record EnrollmentCreatedIntegrationEvent(Guid EnrollmentId, Guid StudentId, Guid CourseId, Guid TenantId = default);

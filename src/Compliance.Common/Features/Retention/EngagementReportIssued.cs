using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

/// <summary>Records the EngagementReportIssued domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.retention.engagement-report-issued", 1)]
public sealed record EngagementReportIssued(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;

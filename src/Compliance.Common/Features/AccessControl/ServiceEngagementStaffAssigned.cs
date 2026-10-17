using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Records the ServiceEngagementStaffAssigned domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.accesscontrol.service-engagement-staff-assigned", 1)]
public sealed record ServiceEngagementStaffAssigned(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;

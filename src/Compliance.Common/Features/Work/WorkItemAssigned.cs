using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Accountability for a work item moved by claim, assign, reassign, or delegate.</summary>
[Discriminator("bdgrz.work.assigned", 1)]
public sealed record WorkItemAssigned(Uuid TenantId, Uuid ProgramId, Uuid WorkItemId,
    long Revision, string Action, Uuid AssigneeMemberId, Uuid? PreviousAssigneeMemberId,
    string? Reason, ActorReference AssignedBy, DateTimeOffset AssignedAt) : DomainEvent;

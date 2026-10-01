using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Opens a request for one piece of evidence, owned by a member and due on a date.</summary>
[Discriminator("bdgrz.evidence.request.opened", 1)]
public sealed record EvidenceRequestOpened(Uuid TenantId, Uuid ProgramId, Uuid EvidenceRequestId,
    string Title, string Instructions, Uuid OwnerMemberId, DateOnly DueOn, Uuid? ControlId,
    ActorReference RequestedBy, DateTimeOffset OpenedAt) : DomainEvent;

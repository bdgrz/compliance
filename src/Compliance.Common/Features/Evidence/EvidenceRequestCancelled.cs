using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Cancels an open request with an attributed rationale.</summary>
[Discriminator("bdgrz.evidence.request.cancelled", 1)]
public sealed record EvidenceRequestCancelled(Uuid TenantId, Uuid ProgramId, Uuid EvidenceRequestId,
    long Revision, string Rationale, ActorReference CancelledBy, DateTimeOffset CancelledAt) : DomainEvent;

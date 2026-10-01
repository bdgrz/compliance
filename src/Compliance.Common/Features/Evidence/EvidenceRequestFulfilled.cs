using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Fulfils a request by linking the evidence artifact that answers it.</summary>
[Discriminator("bdgrz.evidence.request.fulfilled", 1)]
public sealed record EvidenceRequestFulfilled(Uuid TenantId, Uuid ProgramId, Uuid EvidenceRequestId,
    long Revision, Uuid ArtifactId, ActorReference FulfilledBy, DateTimeOffset FulfilledAt) : DomainEvent;

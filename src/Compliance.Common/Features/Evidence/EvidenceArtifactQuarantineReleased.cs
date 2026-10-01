using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Records an attributed decision that a malware quarantine was a false positive.</summary>
[Discriminator("bdgrz.evidence.artifact.quarantine_released", 1)]
public sealed record EvidenceArtifactQuarantineReleased(Uuid TenantId, Uuid ArtifactId,
    string Rationale, ActorReference DecidedBy, DateTimeOffset DecidedAt) : DomainEvent;

using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Releases a malware-quarantined artifact after a clean rescan and an attributed admin decision.</summary>
[Discriminator("bdgrz.evidence.artifact.quarantine.release", 1)]
public sealed record ReleaseEvidenceArtifactQuarantine(Uuid TenantId, Uuid ArtifactId, string Rationale)
    : IRequest, IClientManagementMutationRequest, ICallable;

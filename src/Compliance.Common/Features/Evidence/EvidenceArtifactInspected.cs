using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Records the inspection result that moves an artifact out of pending inspection.</summary>
[Discriminator("bdgrz.evidence.artifact.inspected", 1)]
public sealed record EvidenceArtifactInspected(Uuid TenantId, Uuid ArtifactId, string State,
    string? Reason, DateTimeOffset InspectedAt) : DomainEvent;

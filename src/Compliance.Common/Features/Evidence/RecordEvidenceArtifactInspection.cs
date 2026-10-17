using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>
///     Internal infrastructure result from a trusted artifact inspector. This is intentionally not callable by a
///     product member: callers must not be able to assert that uploaded content is clean.
/// </summary>
[Discriminator("bdgrz.evidence.artifact.inspection.record", 1)]
public sealed record RecordEvidenceArtifactInspection(Uuid TenantId, Uuid ArtifactId,
    EvidenceInspectionOutcome Outcome, DateTimeOffset InspectedAt) : IRequest;

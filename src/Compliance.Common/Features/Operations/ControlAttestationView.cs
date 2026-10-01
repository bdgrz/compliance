using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     One immutable attestation version. A correction is a new version that supersedes the prior
///     one; the performer of record is a member or a workforce person, and the recorder is the
///     signed-in member who entered it.
/// </summary>
public sealed record ControlAttestationView(Uuid AttestationId, int Version, string Result,
    DateTimeOffset PerformedAt, DateOnly? CoveredFrom, DateOnly? CoveredUntil, string? Notes,
    string? Rationale, IReadOnlyList<EvidenceReference> Evidence, OperatingHolder PerformedBy,
    Uuid RecorderMemberId, ActorReference RecordedBy, DateTimeOffset RecordedAt,
    Uuid ControlVersionId, Uuid PlanVersionId, IReadOnlyList<string> ExpectedEvidence,
    string? CorrectionReason = null, Uuid? SupersedesAttestationId = null);

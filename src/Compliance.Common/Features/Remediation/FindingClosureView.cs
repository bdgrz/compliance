using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>The verification and closure review decision for a finding.</summary>
public sealed record FindingClosureView(Uuid DecisionId, string VerificationRationale,
    IReadOnlyList<EvidenceReference> ResolutionEvidence, string Rationale, Uuid CloserMemberId,
    ActorReference ClosedBy, DateTimeOffset ClosedAt, Uuid? SeparationOfDutiesWaiverId = null);

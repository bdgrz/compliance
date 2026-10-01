using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     The independent review decision on one exact attestation version: approved, returned,
///     action_requested, or deferred. Requested actions become findings.
/// </summary>
public sealed record ControlOccurrenceReviewView(Uuid DecisionId, Uuid AttestationId,
    int AttestationVersion, string Outcome, string Rationale,
    IReadOnlyList<string> RequestedActions, Uuid ReviewerMemberId, ActorReference ReviewedBy,
    DateTimeOffset ReviewedAt, Uuid? SeparationOfDutiesWaiverId = null);

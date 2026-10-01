using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     The attributable Type I entry decision on one frozen readiness assessment: approve,
///     approve_with_exceptions, or defer. It binds the exact rule version, as-of time, and input
///     fingerprint, and freezes every unresolved item with its acknowledgement. Internal
///     management approval only; it is never an auditor opinion.
/// </summary>
public sealed record TypeIEntryDecisionView(Uuid DecisionId, Uuid AssessmentId,
    string RuleVersion, DateTimeOffset AsOf, string InputFingerprint, string Outcome,
    string Rationale, IReadOnlyList<TypeIEntryUnresolvedItemView> UnresolvedItems,
    Uuid DeciderMemberId, ActorReference DecidedBy, DateTimeOffset DecidedAt,
    Uuid? SeparationOfDutiesWaiverId)
{
    public const string AuditorOpinionNotice =
        "Internal management decision to begin the Type I examination; it is not an auditor opinion.";

    public string Notice { get; init; } = AuditorOpinionNotice;
}

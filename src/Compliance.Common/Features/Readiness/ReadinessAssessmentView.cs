using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>
///     A reproducible readiness assessment: rule version, as-of time, exact inputs and their
///     fingerprint, per-criterion findings, gaps with their current plan, and any management
///     decision and Type I entry decision. Revision is the program readiness ledger revision at read time.
/// </summary>
public sealed record ReadinessAssessmentView(Uuid TenantId, Uuid ProgramId, Uuid AssessmentId,
    long Revision, string RuleVersion, DateTimeOffset AsOf, Uuid? EditionId,
    string InputFingerprint, IReadOnlyList<ReadinessInputView> Inputs,
    IReadOnlyList<ReadinessFindingView> Findings, IReadOnlyList<ReadinessGapView> Gaps,
    int RuleMetCount, int GapCount, ActorReference RunBy, DateTimeOffset RunAt,
    ReadinessDecisionView? Decision, TypeIEntryDecisionView? TypeIEntryDecision = null);

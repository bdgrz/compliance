using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     Accountable work that carries out a risk's treatment: what state it should reach, by when,
///     who is accountable, and what evidence will show it. <c>Status</c> is <c>open</c>,
///     <c>completion_submitted</c>, or <c>completed</c>; only an independently accepted completion
///     whose evidence requests are fulfilled makes the action <c>completed</c>. <c>Overdue</c> is
///     evaluated at read time.
/// </summary>
public sealed record RiskTreatmentActionView(Uuid ActionId, Uuid RiskId, string Title,
    string TargetState, string ExpectedEvidence, DateOnly DueOn, Uuid AccountableMemberId,
    IReadOnlyList<Uuid> EvidenceRequestIds, string Status, bool Overdue,
    ActorReference CreatedBy, DateTimeOffset CreatedAt,
    IReadOnlyList<RiskTreatmentActionCompletionView> Completions);

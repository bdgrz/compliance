using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     An attributable assertion that an exact approved control version treats a risk under
///     <c>mitigate</c>. Status is proposed, accepted, rejected, superseded, or retired; only an
///     independently accepted assertion permits a residual assessment under <c>mitigate</c>. It
///     never states that the control operates or that the risk is reduced.
/// </summary>
public sealed record RiskControlTreatmentView(Uuid TreatmentId, Uuid RiskId, Uuid ControlId,
    Uuid ControlVersionId, string Status, string Rationale, ActorReference ProposedBy,
    DateTimeOffset ProposedAt, Uuid? ReviewDecisionId, ActorReference? ReviewedBy,
    string? ReviewRationale, DateTimeOffset? ReviewedAt, Uuid? SeparationOfDutiesWaiverId,
    ActorReference? RetiredBy, string? RetirementRationale, DateTimeOffset? RetiredAt);

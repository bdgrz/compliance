using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

[Discriminator("bdgrz.risk.treatment_action.revised", 1)]
public sealed record RiskTreatmentActionRevised(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long Revision, Uuid ActionId, Uuid RequestId, long ExpectedRevision, string Title,
    string TargetState, string ExpectedEvidence, DateOnly DueOn, Uuid AccountableMemberId,
    IReadOnlyList<Uuid> EvidenceRequestIds, ActorReference RevisedBy,
    DateTimeOffset RevisedAt) : DomainEvent;

using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

[Discriminator("bdgrz.risk.treatment_action.completion_submitted", 1)]
public sealed record RiskTreatmentActionCompletionSubmitted(Uuid TenantId, Uuid ProgramId,
    Uuid RiskId, long Revision, Uuid ActionId, Uuid SubmissionId, string Summary,
    IReadOnlyList<Uuid> EvidenceRequestIds, Uuid SubmitterMemberId, ActorReference SubmittedBy,
    DateTimeOffset SubmittedAt) : DomainEvent;

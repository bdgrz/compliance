using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

[Discriminator("bdgrz.risk.treatment_action.cancelled", 1)]
public sealed record RiskTreatmentActionCancelled(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long Revision, Uuid ActionId, Uuid CancellationId, string Rationale,
    ActorReference CancelledBy, DateTimeOffset CancelledAt) : DomainEvent;

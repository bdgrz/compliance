using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

[Discriminator("bdgrz.readiness.decision.recorded", 1)]
public sealed record ReadinessDecisionRecorded(Uuid TenantId, Uuid ProgramId, long Revision,
    ReadinessDecisionView Decision) : DomainEvent;

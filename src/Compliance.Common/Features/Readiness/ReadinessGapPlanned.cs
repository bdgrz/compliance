using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

[Discriminator("bdgrz.readiness.gap.planned", 1)]
public sealed record ReadinessGapPlanned(Uuid TenantId, Uuid ProgramId, long Revision,
    ReadinessGapPlanView Plan) : DomainEvent;

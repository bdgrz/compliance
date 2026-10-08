using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.independence.service-reevaluated", 1)]
public sealed record ServiceIndependenceReevaluated(Uuid TenantId, long ExpectedSequence,
    ServiceIndependenceReevaluationView Reevaluation) : DomainEvent;

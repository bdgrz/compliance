using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.independence.assignment-reevaluated", 1)]
public sealed record AssignmentIndependenceReevaluated(Uuid TenantId, long ExpectedSequence,
    AssignmentIndependenceReevaluationView Reevaluation) : DomainEvent;

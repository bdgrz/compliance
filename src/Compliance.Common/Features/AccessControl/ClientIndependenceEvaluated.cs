using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.independence.evaluated", 1)]
public sealed record ClientIndependenceEvaluated(Uuid TenantId, Uuid RequestId, long ExpectedSequence,
    IndependenceEvaluationView Evaluation) : DomainEvent;

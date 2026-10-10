using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.service-engagement.partner-evaluation.recorded", 1)]
public sealed record PartnerIndependenceEvaluationRecorded(Uuid RequestId, long ExpectedSequence,
    PartnerIndependenceEvaluationView Evaluation, IndependenceRuleVersionView RatifiedRules)
    : DomainEvent;

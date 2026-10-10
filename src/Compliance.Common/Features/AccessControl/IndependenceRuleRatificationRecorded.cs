using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.independence.rules.ratification-recorded", 1)]
public sealed record IndependenceRuleRatificationRecorded(Uuid RequestId, long ExpectedSequence,
    IndependenceRuleRatificationView Ratification) : DomainEvent;

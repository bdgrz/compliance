using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.independence.rules.revised", 1)]
public sealed record IndependenceRulesRevised(Uuid RequestId, long ExpectedSequence,
    IndependenceRuleVersionView Version) : DomainEvent;

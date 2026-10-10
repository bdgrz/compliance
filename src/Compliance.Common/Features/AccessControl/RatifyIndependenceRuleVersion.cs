using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.independence.rules.ratify", 1)]
public sealed record RatifyIndependenceRuleVersion(Uuid RatificationId, long RuleVersion,
    long ExpectedRuleCatalogSequence, long ExpectedRatificationSequence, string ExpectedContentDigest,
    string SourceReference) : IRequest<IndependenceRuleRatificationView>, IPersonalRuleRatificationRequest, ICallable;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed record IndependenceRuleContent(int LookBackMonths,
    IReadOnlyList<IndependenceServiceRuleContent> ServiceRules, string SourceReference);

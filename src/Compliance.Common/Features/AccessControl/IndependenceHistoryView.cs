using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed record IndependenceHistoryView(Uuid TenantId, long Sequence,
    IReadOnlyList<IndependenceRuleVersionView> RuleVersions,
    IReadOnlyList<NonattestServiceView> Services, IReadOnlyList<IndependenceEvaluationView> Evaluations);

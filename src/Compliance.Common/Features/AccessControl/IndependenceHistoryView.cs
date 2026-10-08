using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed record IndependenceHistoryView(Uuid TenantId, long Sequence,
    IReadOnlyList<IndependenceRuleVersionView> RuleVersions,
    IReadOnlyList<NonattestServiceView> Services, IReadOnlyList<IndependenceEvaluationView> Evaluations)
{
    readonly IReadOnlyList<ServiceIndependenceReevaluationView> _sourceReevaluations =
        Array.Empty<ServiceIndependenceReevaluationView>();

    public IReadOnlyList<ServiceIndependenceReevaluationView> SourceReevaluations
    {
        get => _sourceReevaluations;
        init => _sourceReevaluations = Array.AsReadOnly((value ?? []).ToArray());
    }
}

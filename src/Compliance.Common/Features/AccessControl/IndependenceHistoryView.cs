using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed record IndependenceHistoryView(Uuid TenantId, long Sequence,
    IReadOnlyList<IndependenceRuleVersionView> RuleVersions,
    IReadOnlyList<NonattestServiceView> Services, IReadOnlyList<IndependenceEvaluationView> Evaluations)
{
    readonly IReadOnlyList<ServiceIndependenceReevaluationView> _sourceReevaluations =
        Array.Empty<ServiceIndependenceReevaluationView>();
    readonly IReadOnlyList<AssignmentIndependenceReevaluationView> _assignmentReevaluations =
        Array.Empty<AssignmentIndependenceReevaluationView>();

    readonly IReadOnlyList<DirectoryIndependenceReevaluationView> _directoryReevaluations =
        Array.Empty<DirectoryIndependenceReevaluationView>();
    readonly IReadOnlyList<PartnerIndependenceEvaluationView> _partnerEvaluations =
        Array.Empty<PartnerIndependenceEvaluationView>();

    public IReadOnlyList<PartnerIndependenceEvaluationView> PartnerEvaluations
    {
        get => _partnerEvaluations;
        init => _partnerEvaluations = Array.AsReadOnly((value ?? []).ToArray());
    }

    public IReadOnlyList<DirectoryIndependenceReevaluationView> DirectoryReevaluations
    {
        get => _directoryReevaluations;
        init => _directoryReevaluations = Array.AsReadOnly((value ?? []).ToArray());
    }

    public IReadOnlyList<ServiceIndependenceReevaluationView> SourceReevaluations
    {
        get => _sourceReevaluations;
        init => _sourceReevaluations = Array.AsReadOnly((value ?? []).ToArray());
    }

    public IReadOnlyList<AssignmentIndependenceReevaluationView> AssignmentReevaluations
    {
        get => _assignmentReevaluations;
        init => _assignmentReevaluations = Array.AsReadOnly((value ?? []).ToArray());
    }
}

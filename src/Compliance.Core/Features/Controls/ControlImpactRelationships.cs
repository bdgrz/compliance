using Bdgrz.Compliance.Features.Evaluations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>Resolves retained finding relationships through canonical IDs and the owning control identifier.</summary>
static class ControlImpactRelationships
{
    public static IReadOnlyList<FindingView> Findings(ControlDraft subject,
        ControlOperationsLedger operations, IReadOnlyList<ControlEvaluationView> evaluations,
        RemediationLedger remediation)
    {
        var evaluationIds = evaluations.Select(static evaluation => evaluation.EvaluationId).ToHashSet();
        var deviationIds = evaluations.SelectMany(static evaluation => evaluation.Deviations)
            .Select(static deviation => deviation.DeviationId).ToHashSet();
        return [.. remediation.ReadAll(DateTimeOffset.MinValue).Where(finding =>
            finding.Links.Any(MatchesLink) ||
            finding.Source.RecordId is { } sourceId &&
            (finding.Source.Kind is "control_occurrence" or "occurrence_review" &&
             operations.OccurrenceControlId(sourceId) == subject.Id ||
             finding.Source.Kind == "evaluation_deviation" && deviationIds.Contains(sourceId)))];

        bool MatchesLink(FindingLink link)
        {
            if (link.Kind == "control" &&
                ControlDraft.IdFor(subject.TenantId, subject.ProgramId, link.Reference) == subject.Id)
                return true;
            if (!Uuid.TryParse(link.Reference, null, out var id))
                return false;
            return link.Kind switch
            {
                "control" => id == subject.Id,
                "control_occurrence" => operations.OccurrenceControlId(id) == subject.Id,
                "control_evaluation" => evaluationIds.Contains(id),
                _ => false,
            };
        }
    }
}

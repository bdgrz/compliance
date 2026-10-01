namespace Bdgrz.Compliance.Features.Risks;

/// <summary>Evaluates time-dependent status: acceptance expiry and annual reassessment.</summary>
public static class RiskEvaluationStatus
{
    public static RiskEvaluationView AsOf(RiskEvaluationView view, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(view);
        var acceptances = view.Acceptances
            .Select(acceptance => acceptance with
            {
                Status = now >= acceptance.ExpiresAt ? "expired" : "active",
            })
            .ToList();
        if (view.Assessments.Count == 0)
            return view with { Status = "unassessed", Acceptances = acceptances, ReassessmentDueAt = null };
        var due = view.Assessments.Max(assessment => assessment.AssessedAt).AddYears(1);
        var residual = view.Assessments.LastOrDefault(assessment =>
            assessment.Phase == RiskEvaluation.Residual);
        var acceptance = residual is null
            ? null
            : acceptances.LastOrDefault(item => item.ResidualAssessmentId == residual.AssessmentId);
        var triggered = view.OpenReassessmentTriggers is { Count: > 0 };
        var status = (now >= due || triggered, view.Treatment, residual, acceptance) switch
        {
            (true, _, _, _) => "reassessment_due",
            (_, null, _, _) => "assessed",
            (_, _, null, _) => "treatment_chosen",
            (_, { Kind: "accept" }, _, { Status: "active" }) => "accepted",
            (_, { Kind: "accept" }, _, { Status: "expired" }) => "reassessment_due",
            (_, { Kind: "accept" }, _, null) => "acceptance_pending",
            _ => "residual_assessed",
        };
        return view with { Status = status, Acceptances = acceptances, ReassessmentDueAt = due };
    }
}

using System.Globalization;
using Bdgrz.Compliance.Features.Evaluations;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>
///     Reports retained operating plans, occurrences, evaluations, corrective work and evidence
///     requests from their authoritative program streams. Cadence plans represent future work;
///     no actor visibility or queue date horizon can hide a retained relationship.
/// </summary>
public sealed class WorkControlImpactContributor(IAggregateReader reader)
    : IControlImpactContributor
{
    public string Context => "work";

    public async ValueTask<Result<ControlImpactContribution>> ContributeAsync(
        ControlDraft subject, IReadOnlyList<ControlChange> changes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(subject);
        var operations = await reader.HydrateAsync(new ControlOperationsLedger(subject.TenantId,
            subject.ProgramId), ct).ConfigureAwait(false);
        var evaluations = await reader.HydrateAsync(new ControlEvaluationLedger(subject.TenantId,
            subject.ProgramId), ct).ConfigureAwait(false);
        var remediation = await reader.HydrateAsync(new RemediationLedger(subject.TenantId,
            subject.ProgramId), ct).ConfigureAwait(false);
        var evidence = await reader.HydrateAsync(new EvidenceRequestLedger(subject.TenantId,
            subject.ProgramId), ct).ConfigureAwait(false);
        var records = new List<ControlAffectedRecord>();
        foreach (var plan in operations.ReadPlans(subject.Id).History)
            records.Add(new ControlAffectedRecord(subject.TenantId, Context,
                "control_operating_plan", plan.PlanVersionId, plan.ControlVersionId,
                string.Create(CultureInfo.InvariantCulture,
                    $"The {plan.Status} operating plan (revision {plan.Revision}) retains this Control version and cadence: {plan.CadenceDescription}. Review future work after the decision.")));
        foreach (var occurrence in operations.ReadRecordedOccurrences(subject.Id))
            records.Add(new ControlAffectedRecord(subject.TenantId, Context,
                "control_occurrence", occurrence.OccurrenceId, occurrence.ControlVersionId,
                string.Create(CultureInfo.InvariantCulture,
                    $"The {occurrence.State} recorded occurrence (revision {occurrence.Revision}) retains this Control version; preserve its operating history.")));
        var linkedEvaluations = evaluations.ReadControl(subject.Id);
        foreach (var evaluation in linkedEvaluations)
            records.Add(new ControlAffectedRecord(subject.TenantId, Context,
                "control_evaluation", evaluation.EvaluationId, evaluation.ControlVersionId,
                string.Create(CultureInfo.InvariantCulture,
                    $"The {evaluation.State} evaluation (revision {evaluation.Revision}) retains this Control version; review its applicability after the decision.")));
        foreach (var finding in ControlImpactRelationships.Findings(subject, operations,
                     linkedEvaluations, remediation))
        {
            records.Add(new ControlAffectedRecord(subject.TenantId, Context,
                "finding", finding.FindingId, null,
                string.Create(CultureInfo.InvariantCulture,
                    $"The {finding.Status} finding (revision {finding.Revision}) retains a relationship to this Control.")));
            foreach (var action in finding.CorrectiveActions)
                records.Add(new ControlAffectedRecord(subject.TenantId, Context,
                    "corrective_action", action.ActionId, null,
                    string.Create(CultureInfo.InvariantCulture,
                        $"The {action.Status} corrective action for finding {finding.FindingId} (revision {finding.Revision}) retains a relationship to this Control.")));
        }
        foreach (var request in evidence.ReadAll().Where(request => request.ControlId == subject.Id))
            records.Add(new ControlAffectedRecord(subject.TenantId, Context,
                "evidence_request", request.EvidenceRequestId, null,
                string.Create(CultureInfo.InvariantCulture,
                    $"The {request.Status} evidence request (revision {request.Revision}) retains collection work for this Control.")));
        return ControlImpactRecords.Contribution(Context, records);
    }
}

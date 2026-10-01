using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>
///     Derives open work from the authoritative source ledgers at read time (M0-D15): control
///     occurrences to perform, attestations awaiting review, and open corrective actions. Nothing
///     here is stored, so completing source work removes the item and a projection-only change can
///     never complete it.
/// </summary>
static class WorkSource
{
    public const string ControlOccurrence = "control_occurrence";
    public const string OccurrenceReview = "occurrence_review";
    public const string CorrectiveAction = "corrective_action";

    public static async ValueTask<IReadOnlyList<WorkCandidate>> LoadAsync(IAggregateReader reader,
        Uuid tenantId, Uuid programId, DateOnly today, DateOnly horizon, DateTimeOffset now,
        CancellationToken ct)
    {
        var candidates = new List<WorkCandidate>();
        var prefix = $"/api/v1/tenants/{tenantId}/programs/{programId}";
        var operations = await reader.HydrateAsync(new ControlOperationsLedger(tenantId,
            programId), ct).ConfigureAwait(false);
        foreach (var controlId in operations.PlannedControlIds.ToArray())
        {
            if (operations.CurrentPlan(controlId) is not { } plan)
                continue;
            var control = await ControlOperationsSource.LoadControlAsync(reader, tenantId,
                programId, controlId, ct).ConfigureAwait(false);
            if (control is null)
                continue;
            var identifier = control.ApprovedVersion?.Identifier ?? controlId.ToString();
            foreach (var occurrence in operations.ReadOccurrences(controlId,
                         OperatingAuthority.VersionWindows(control), today, horizon))
            {
                var path = $"{prefix}/controls/{controlId}/occurrences/{occurrence.OccurrenceId}";
                var period = occurrence.PeriodStart is { } start
                    ? $"{start:yyyy-MM-dd}"
                    : occurrence.OccurrenceId.ToString();
                var created = occurrence.PeriodStart is { } from
                    ? new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
                    : DateTimeOffset.MinValue;
                if (occurrence.State is ControlOperationsLedger.Expected or
                    ControlOperationsLedger.Missed or ControlOperationsLedger.Open or
                    ControlOperationsLedger.Returned)
                    candidates.Add(new WorkCandidate(
                        WorkCandidate.IdFor(occurrence.OccurrenceId, ControlOccurrence),
                        ControlOccurrence, occurrence.OccurrenceId, controlId, null,
                        $"Perform {identifier} for {period}",
                        "The approved operating plan expects this control to operate for the period.",
                        occurrence.DueOn, null, "attest", path + "/attestations",
                        occurrence.Assignee, plan.BackupOwner, new HashSet<Uuid>(), created));
                if (occurrence.State is ControlOperationsLedger.Submitted or
                        ControlOperationsLedger.Deferred && occurrence.Attestations.Count > 0)
                {
                    var attestation = occurrence.Attestations[^1];
                    var excluded = new HashSet<Uuid> { attestation.RecorderMemberId };
                    if (attestation.PerformedBy is { Kind: OperatingAuthority.MemberHolder } performer)
                        excluded.Add(performer.Id);
                    candidates.Add(new WorkCandidate(
                        WorkCandidate.IdFor(occurrence.OccurrenceId, OccurrenceReview),
                        OccurrenceReview, occurrence.OccurrenceId, controlId, null,
                        $"Review {identifier} for {period}",
                        "An attestation awaits independent review by someone who did not perform or record it.",
                        occurrence.DueOn, null, "review", path + "/reviews",
                        new OperatingHolder(OperatingAuthority.MemberHolder, plan.ReviewerMemberId),
                        null, excluded, attestation.RecordedAt));
                }
            }
        }
        var remediation = await reader.HydrateAsync(new RemediationLedger(tenantId, programId), ct)
            .ConfigureAwait(false);
        foreach (var finding in remediation.ReadAll(now)
                     .Where(static finding => finding.Status != RemediationLedger.Closed))
            foreach (var action in finding.CorrectiveActions.Where(static action =>
                         action.Status == RemediationLedger.Open))
                candidates.Add(new WorkCandidate(
                    WorkCandidate.IdFor(action.ActionId, CorrectiveAction), CorrectiveAction,
                    action.ActionId, null, finding.FindingId, action.Description,
                    $"Corrective action for finding \"{finding.Title}\".", action.DueOn,
                    RemediationLedger.Materiality(finding.Severity), "complete",
                    $"{prefix}/findings/{finding.FindingId}/corrective-actions/{action.ActionId}/completions",
                    new OperatingHolder(OperatingAuthority.MemberHolder, action.OwnerMemberId),
                    null, new HashSet<Uuid>(), action.AddedAt));
        return candidates;
    }
}

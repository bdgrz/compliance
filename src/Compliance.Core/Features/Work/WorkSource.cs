using System.Globalization;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.ControlMappings;
using Bdgrz.Compliance.Features.Evaluations;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Risks;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>
///     Derives open work from the authoritative source ledgers at read time (M0-D15): control
///     occurrences to perform; control-plan approvals; control-attestation, control-mapping,
///     criterion-applicability, evaluation, boundary, and risk-treatment reviews; corrective
///     actions; and evidence requests. Nothing here is stored, so completing source work removes
///     the item and a projection-only change can never complete it.
/// </summary>
static class WorkSource
{
    public const string ControlOccurrence = "control_occurrence";
    public const string OccurrenceReview = "occurrence_review";
    public const string CorrectiveAction = "corrective_action";
    public const string EvidenceRequest = "evidence_request";
    public const string RiskTreatmentAction = "risk_treatment_action";
    public const string RiskTreatmentActionReview = "risk_treatment_action_review";
    public const string RiskControlTreatmentReview = "risk_control_treatment_review";
    public const string ControlEvaluationReview = "control_evaluation_review";
    public const string ControlOperatingPlanApproval = "control_operating_plan_approval";
    public const string ControlCriterionMappingReview = "control_criterion_mapping_review";
    public const string CriterionApplicabilityReview = "criterion_applicability_review";

    /// <summary>Loads open work; <paramref name="workItemId" /> narrows the result to one item.</summary>
    public static async ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadAsync(
        IAggregateReader reader,
        Uuid tenantId, Uuid programId, DateOnly today, DateOnly horizon, DateTimeOffset now,
        Uuid? workItemId, IBoundaryDirectoryReader? boundaries,
        BoundaryDirectoryReadConsistency? boundaryConsistency, CancellationToken ct)
    {
        bool Wanted(Uuid sourceId, string kind) =>
            workItemId is not { } wanted || WorkCandidate.IdFor(sourceId, kind) == wanted;
        var candidates = new List<WorkCandidate>();
        var prefix = $"/api/v1/tenants/{tenantId}/programs/{programId}";
        var operations = await reader.HydrateAsync(new ControlOperationsLedger(tenantId,
            programId), ct).ConfigureAwait(false);
        foreach (var (controlId, plan, identifier, occurrences) in await ControlOperationsSource
                     .ReadPlannedAsync(reader, operations, tenantId, programId, today, horizon, ct)
                     .ConfigureAwait(false))
            foreach (var occurrence in occurrences)
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
                        ControlOperationsLedger.Returned &&
                    Wanted(occurrence.OccurrenceId, ControlOccurrence))
                    candidates.Add(new WorkCandidate(
                        WorkCandidate.IdFor(occurrence.OccurrenceId, ControlOccurrence),
                        ControlOccurrence, occurrence.OccurrenceId, controlId, null,
                        $"Perform {identifier} for {period}",
                        "The approved operating plan expects this control to operate for the period.",
                        occurrence.DueOn, null, "attest", path + "/attestations",
                        occurrence.Assignee, plan.BackupOwner, new HashSet<Uuid>(), created));
                if (occurrence.State is ControlOperationsLedger.Submitted or
                        ControlOperationsLedger.Deferred && occurrence.Attestations.Count > 0 &&
                    Wanted(occurrence.OccurrenceId, OccurrenceReview))
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
        var remediation = await reader.HydrateAsync(new RemediationLedger(tenantId, programId), ct)
            .ConfigureAwait(false);
        foreach (var finding in remediation.ReadAll(now)
                     .Where(static finding => finding.Status != RemediationLedger.Closed))
            foreach (var action in finding.CorrectiveActions.Where(action =>
                         action.Status == RemediationLedger.Open &&
                         Wanted(action.ActionId, CorrectiveAction)))
                candidates.Add(new WorkCandidate(
                    WorkCandidate.IdFor(action.ActionId, CorrectiveAction), CorrectiveAction,
                    action.ActionId, null, finding.FindingId, action.Description,
                    $"Corrective action for finding \"{finding.Title}\".", action.DueOn,
                    RemediationLedger.Materiality(finding.Severity), "complete",
                    $"{prefix}/findings/{finding.FindingId}/corrective-actions/{action.ActionId}/completions",
                    new OperatingHolder(OperatingAuthority.MemberHolder, action.OwnerMemberId),
                    null, new HashSet<Uuid>(), action.AddedAt));
        var governance = await reader.HydrateAsync(new RiskGovernanceLedger(tenantId, programId),
            ct).ConfigureAwait(false);
        foreach (var action in governance.Actions().Where(action =>
                     action.Status == RiskGovernanceLedger.ActionOpen &&
                     Wanted(action.ActionId, RiskTreatmentAction)))
            candidates.Add(new WorkCandidate(
                WorkCandidate.IdFor(action.ActionId, RiskTreatmentAction), RiskTreatmentAction,
                action.ActionId, null, null, action.Title,
                $"Treatment work for a risk. Target state: {action.TargetState}", action.DueOn,
                null, "complete",
                $"{prefix}/risks/{action.RiskId}/treatment-actions/{action.ActionId}/completions",
                new OperatingHolder(OperatingAuthority.MemberHolder, action.AccountableMemberId),
                null, new HashSet<Uuid>(), action.CreatedAt));
        foreach (var action in governance.Actions().Where(action =>
                     action.Status == RiskGovernanceLedger.ActionSubmitted))
        {
            var completion = action.Completions.LastOrDefault(static item =>
                item.ReviewOutcome is null);
            if (completion is null || !Wanted(completion.SubmissionId, RiskTreatmentActionReview))
                continue;
            var excluded = new HashSet<Uuid> { action.AccountableMemberId };
            if (governance.SubmitterMemberId(action.RiskId, completion.SubmissionId) is { } submitter)
                excluded.Add(submitter);
            candidates.Add(new WorkCandidate(
                WorkCandidate.IdFor(completion.SubmissionId, RiskTreatmentActionReview),
                RiskTreatmentActionReview, completion.SubmissionId, null, null,
                $"Review completion for {action.Title}",
                "A risk treatment action completion is awaiting independent review.", action.DueOn,
                null, "review",
                $"{prefix}/risks/{action.RiskId}/treatment-actions/{action.ActionId}/completion-reviews",
                new OperatingHolder(OperatingAuthority.ProgramReviewerHolder, programId), null,
                excluded, completion.SubmittedAt));
        }
        foreach (var treatment in governance.PendingControlTreatments())
        {
            var identity = Uuid.CreateVersion5(treatment.RiskId,
                $"risk-control-treatment-review\n{treatment.TreatmentId}");
            var candidateId = WorkCandidate.IdFor(identity, RiskControlTreatmentReview);
            if (workItemId is { } wantedTreatment && candidateId != wantedTreatment)
                continue;
            var proposerMemberId = Uuid.Parse(treatment.ProposedBy.Id,
                CultureInfo.InvariantCulture);
            candidates.Add(new WorkCandidate(candidateId, RiskControlTreatmentReview,
                treatment.TreatmentId, treatment.ControlId, null,
                $"Review control treatment for risk {treatment.RiskId}",
                "A risk control-treatment assertion is awaiting independent review.", null, null,
                "review",
                $"{prefix}/risks/{treatment.RiskId}/control-treatments/" +
                $"{treatment.TreatmentId}/reviews",
                new OperatingHolder(OperatingAuthority.ProgramReviewerHolder, programId), null,
                new HashSet<Uuid> { proposerMemberId }, treatment.ProposedAt));
        }
        var evidence = await reader.HydrateAsync(new EvidenceRequestLedger(tenantId, programId), ct)
            .ConfigureAwait(false);
        foreach (var request in evidence.ReadAll().Where(request =>
                     request.Status == EvidenceRequestLedger.Open && Wanted(request.EvidenceRequestId, EvidenceRequest)))
            candidates.Add(new WorkCandidate(
                WorkCandidate.IdFor(request.EvidenceRequestId, EvidenceRequest), EvidenceRequest,
                request.EvidenceRequestId, request.ControlId, null, request.Title, request.Instructions,
                request.DueOn, null, "fulfil",
                $"{prefix}/evidence-requests/{request.EvidenceRequestId}/fulfilments",
                new OperatingHolder(OperatingAuthority.MemberHolder, request.OwnerMemberId), null,
                new HashSet<Uuid>(), request.OpenedAt));
        var evaluations = await reader.HydrateAsync(new ControlEvaluationLedger(tenantId, programId), ct)
            .ConfigureAwait(false);
        foreach (var evaluation in evaluations.ReadAwaitingReview())
        {
            var identity = Uuid.CreateVersion5(evaluation.EvaluationId,
                $"control-evaluation-review\n{evaluation.Round}");
            var candidateId = WorkCandidate.IdFor(identity, ControlEvaluationReview);
            if (workItemId is { } wanted && candidateId != wanted)
                continue;
            var submission = evaluation.Submissions[^1];
            candidates.Add(new WorkCandidate(candidateId, ControlEvaluationReview,
                evaluation.EvaluationId, evaluation.ControlId, null,
                "Review control evaluation",
                $"Control evaluation round {evaluation.Round} is awaiting independent review.",
                null, null, "review",
                $"{prefix}/controls/{evaluation.ControlId}/evaluations/{evaluation.EvaluationId}/reviews",
                new OperatingHolder(OperatingAuthority.ProgramReviewerHolder, programId), null,
                new HashSet<Uuid> { evaluation.EvaluatorMemberId }, submission.SubmittedAt));
        }
        var mappings = await reader.HydrateAsync(new ControlCriterionMappingLedger(tenantId,
            programId), ct).ConfigureAwait(false);
        foreach (var mapping in mappings.ReadAll().Where(static mapping =>
                     mapping.Status == "pending" &&
                     mapping.Versions.Count > 0 && mapping.Versions[^1].Status == "proposed"))
        {
            var proposal = mapping.Versions[^1];
            var identity = Uuid.CreateVersion5(mapping.MappingId,
                $"control-criterion-mapping-review\n{proposal.VersionNumber}");
            var candidateId = WorkCandidate.IdFor(identity, ControlCriterionMappingReview);
            if (workItemId is { } wantedMapping && candidateId != wantedMapping)
                continue;
            var proposerMemberId = Uuid.Parse(proposal.ProposedBy.Id, CultureInfo.InvariantCulture);
            candidates.Add(new WorkCandidate(candidateId, ControlCriterionMappingReview,
                mapping.MappingId, mapping.ControlId, null,
                $"Review control mapping for {mapping.CriterionIdentifier}",
                "A control-to-criteria mapping proposal is awaiting independent review.", null,
                null, "review",
                $"{prefix}/control-mappings/{mapping.MappingId}/reviews",
                new OperatingHolder(OperatingAuthority.ProgramReviewerHolder, programId), null,
                new HashSet<Uuid> { proposerMemberId }, proposal.ProposedAt));
        }
        var applicability = await reader.HydrateAsync(new CriterionApplicabilityLedger(tenantId,
            programId), ct).ConfigureAwait(false);
        foreach (var decision in applicability.ReadAll().Where(static decision =>
                     decision.Status == "pending" &&
                     decision.Versions.Count > 0 &&
                     decision.Versions[^1].Status == "proposed"))
        {
            var proposal = decision.Versions[^1];
            var identity = Uuid.CreateVersion5(decision.DecisionId,
                $"criterion-applicability-review\n{proposal.VersionNumber}");
            var candidateId = WorkCandidate.IdFor(identity, CriterionApplicabilityReview);
            if (workItemId is { } wantedApplicability && candidateId != wantedApplicability)
                continue;
            var proposerMemberId = Uuid.Parse(proposal.ProposedBy.Id, CultureInfo.InvariantCulture);
            candidates.Add(new WorkCandidate(candidateId, CriterionApplicabilityReview,
                decision.DecisionId, null, null,
                $"Review not-applicable proposal for {decision.CriterionIdentifier}",
                "A criterion not-applicable proposal is awaiting independent review.", null, null,
                "review", $"{prefix}/criterion-applicability/{decision.DecisionId}/reviews",
                new OperatingHolder(OperatingAuthority.ProgramReviewerHolder, programId), null,
                new HashSet<Uuid> { proposerMemberId }, proposal.ProposedAt));
        }
        foreach (var controlId in operations.PlannedControlIds)
        {
            if (operations.PendingPlan(controlId) is not { } pending ||
                workItemId is { } wantedPlan && WorkCandidate.IdFor(pending.PlanVersionId,
                    ControlOperatingPlanApproval) != wantedPlan)
                continue;
            var control = await ControlOperationsSource.LoadControlAsync(reader, tenantId,
                programId, controlId, ct).ConfigureAwait(false);
            if (control is null ||
                control.ApprovedVersion is not { Status: ControlOperationsLedger.Approved } current ||
                current.VersionId != pending.ControlVersionId)
                continue;
            var actionPath = $"{prefix}/controls/{controlId}/operating-plan/proposals/" +
                             $"{pending.PlanVersionId}/approvals";
            candidates.Add(new WorkCandidate(
                WorkCandidate.IdFor(pending.PlanVersionId, ControlOperatingPlanApproval),
                ControlOperatingPlanApproval, pending.PlanVersionId, controlId, null,
                $"Approve operating plan for {current.Identifier}",
                "A control operating plan is awaiting independent approval.", null, null,
                "approve", actionPath,
                new OperatingHolder(OperatingAuthority.ProgramReviewerHolder, programId), null,
                new HashSet<Uuid> { pending.ProposerMemberId }, pending.ProposedAt));
        }
        if (boundaries is not null)
        {
            var consistency = boundaryConsistency ?? throw new InvalidOperationException(
                "Boundary work requires boundary-directory read consistency.");
            var boundaryWork = await BoundaryDecisionWork.LoadAsync(reader, boundaries,
                consistency, tenantId, programId, now, workItemId, ct).ConfigureAwait(false);
            if (!boundaryWork.IsSuccess)
                return Result<IReadOnlyList<WorkCandidate>>.Failure(boundaryWork.Error);
            candidates.AddRange(boundaryWork.Value);
        }
        return Result<IReadOnlyList<WorkCandidate>>.Success(candidates);
    }
}

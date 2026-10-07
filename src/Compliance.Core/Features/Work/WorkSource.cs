using System.Globalization;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.ControlMappings;
using Bdgrz.Compliance.Features.Evaluations;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Risks;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>
///     Builds candidates from authoritative source ledgers at read time for work families without
///     a supplied projection. Work completion remains owned by each source workflow.
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
        IReadOnlySet<string> projectedKinds,
        CancellationToken ct)
    {
        bool Wanted(Uuid sourceId, string kind) =>
            workItemId is not { } wanted || WorkCandidate.IdFor(sourceId, kind) == wanted;
        bool WantedRiskAction(Uuid riskId, Uuid sourceId, string kind) =>
            workItemId is not { } wanted ||
            RiskTreatmentWorkItemId(programId, riskId, sourceId, kind) == wanted;
        var candidates = new List<WorkCandidate>();
        var prefix = $"/api/v1/tenants/{tenantId}/programs/{programId}";
        ControlOperationsLedger? operations = null;
        if (!projectedKinds.Contains(ControlOccurrence) ||
            !projectedKinds.Contains(OccurrenceReview))
        {
            operations = await reader.HydrateAsync(new ControlOperationsLedger(tenantId,
                programId), ct).ConfigureAwait(false);
            foreach (var (controlId, plan, identifier, occurrences) in await ControlOperationsSource
                         .ReadPlannedAsync(reader, operations, tenantId, programId, today, horizon,
                             ct).ConfigureAwait(false))
                foreach (var occurrence in occurrences)
                {
                    if (!projectedKinds.Contains(ControlOccurrence) &&
                        (occurrence.State is ControlOperationsLedger.Expected or
                            ControlOperationsLedger.Missed or ControlOperationsLedger.Open or
                            ControlOperationsLedger.Returned) &&
                        Wanted(occurrence.OccurrenceId, ControlOccurrence))
                        candidates.Add(ControlOccurrenceCandidate(tenantId, programId, controlId,
                            identifier, occurrence.OccurrenceId, occurrence.PeriodStart,
                            occurrence.DueOn, occurrence.Assignee, plan.BackupOwner));

                    if (!projectedKinds.Contains(OccurrenceReview) &&
                        (occurrence.State is ControlOperationsLedger.Submitted or
                            ControlOperationsLedger.Deferred) && occurrence.Attestations.Count > 0 &&
                        Wanted(occurrence.OccurrenceId, OccurrenceReview))
                    {
                        var attestation = occurrence.Attestations[^1];
                        candidates.Add(OccurrenceReviewCandidate(tenantId, programId, controlId,
                            identifier, occurrence.OccurrenceId, occurrence.PeriodStart,
                            occurrence.DueOn, plan.ReviewerMemberId, attestation.PerformedBy,
                            attestation.RecorderMemberId, attestation.RecordedAt));
                    }
                }
        }
        if (!projectedKinds.Contains(FindingClosureWork.Kind))
        {
            var findings = await reader.HydrateAsync(new RemediationLedger(tenantId, programId), ct)
                .ConfigureAwait(false);
            foreach (var finding in findings.ReadAll(now))
                if (FindingClosureWork.Candidate(finding) is { } candidate &&
                    (workItemId is not { } wantedClosure || candidate.WorkItemId == wantedClosure))
                    candidates.Add(candidate);
        }
        if (!projectedKinds.Contains(CorrectiveAction))
        {
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
        }
        if (!projectedKinds.Contains(RiskTreatmentAction) ||
            !projectedKinds.Contains(RiskTreatmentActionReview) ||
            !projectedKinds.Contains(RiskControlTreatmentReview))
        {
            var governance = await reader.HydrateAsync(new RiskGovernanceLedger(tenantId, programId),
                ct).ConfigureAwait(false);
            if (!projectedKinds.Contains(RiskTreatmentAction))
                foreach (var action in governance.Actions().Where(action =>
                             action.Status == RiskGovernanceLedger.ActionOpen &&
                             WantedRiskAction(action.RiskId, action.ActionId,
                                 RiskTreatmentAction)))
                    candidates.Add(new WorkCandidate(
                        RiskTreatmentWorkItemId(programId, action.RiskId, action.ActionId,
                            RiskTreatmentAction),
                        RiskTreatmentAction, action.ActionId, null, null, action.Title,
                        $"Treatment work for a risk. Target state: {action.TargetState}", action.DueOn,
                        null, "complete",
                        $"{prefix}/risks/{action.RiskId}/treatment-actions/{action.ActionId}/completions",
                        new OperatingHolder(OperatingAuthority.MemberHolder,
                            action.AccountableMemberId),
                        null, new HashSet<Uuid>(), action.CreatedAt));
            if (!projectedKinds.Contains(RiskTreatmentActionReview))
                foreach (var action in governance.Actions().Where(action =>
                             action.Status == RiskGovernanceLedger.ActionSubmitted))
                {
                    var completion = action.Completions.LastOrDefault(static item =>
                        item.ReviewOutcome is null);
                    if (completion is null ||
                        !WantedRiskAction(action.RiskId, completion.SubmissionId,
                            RiskTreatmentActionReview))
                        continue;
                    var excluded = new HashSet<Uuid> { action.AccountableMemberId };
                    if (governance.SubmitterMemberId(action.RiskId,
                            completion.SubmissionId) is { } submitter)
                        excluded.Add(submitter);
                    candidates.Add(new WorkCandidate(
                        RiskTreatmentWorkItemId(programId, action.RiskId,
                            completion.SubmissionId, RiskTreatmentActionReview),
                        RiskTreatmentActionReview, completion.SubmissionId, null, null,
                        $"Review completion for {action.Title}",
                        "A risk treatment action completion is awaiting independent review.",
                        action.DueOn, null, "review",
                        $"{prefix}/risks/{action.RiskId}/treatment-actions/" +
                        $"{action.ActionId}/completion-reviews",
                        new OperatingHolder(OperatingAuthority.ProgramReviewerHolder, programId), null,
                        excluded, completion.SubmittedAt));
                }
            if (!projectedKinds.Contains(RiskControlTreatmentReview))
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
                        "A risk control-treatment assertion is awaiting independent review.", null,
                        null, "review",
                        $"{prefix}/risks/{treatment.RiskId}/control-treatments/" +
                        $"{treatment.TreatmentId}/reviews",
                        new OperatingHolder(OperatingAuthority.ProgramReviewerHolder, programId), null,
                        new HashSet<Uuid> { proposerMemberId }, treatment.ProposedAt));
                }
        }
        if (!projectedKinds.Contains(EvidenceRequest))
        {
            var evidence = await reader.HydrateAsync(new EvidenceRequestLedger(tenantId, programId), ct)
                .ConfigureAwait(false);
            foreach (var request in evidence.ReadAll().Where(request =>
                         request.Status == EvidenceRequestLedger.Open &&
                         Wanted(request.EvidenceRequestId, EvidenceRequest)))
                candidates.Add(new WorkCandidate(
                    WorkCandidate.IdFor(request.EvidenceRequestId, EvidenceRequest), EvidenceRequest,
                    request.EvidenceRequestId, request.ControlId, null, request.Title, request.Instructions,
                    request.DueOn, null, "fulfil",
                    $"{prefix}/evidence-requests/{request.EvidenceRequestId}/fulfilments",
                    new OperatingHolder(OperatingAuthority.MemberHolder, request.OwnerMemberId), null,
                    new HashSet<Uuid>(), request.OpenedAt));
        }
        if (!projectedKinds.Contains(ControlEvaluationReview))
        {
            var evaluations = await reader.HydrateAsync(
                new ControlEvaluationLedger(tenantId, programId), ct).ConfigureAwait(false);
            foreach (var evaluation in evaluations.ReadAwaitingReview())
            {
                var submission = evaluation.Submissions[^1];
                var candidate = ControlEvaluationReviewCandidate(tenantId, programId,
                    evaluation.ControlId, evaluation.EvaluationId, evaluation.EvaluatorMemberId,
                    evaluation.Round, submission.SubmittedAt);
                var candidateId = candidate.WorkItemId;
                if (workItemId is { } wanted && candidateId != wanted)
                    continue;
                candidates.Add(candidate);
            }
        }
        if (!projectedKinds.Contains(ControlCriterionMappingReview))
        {
            var mappings = await reader.HydrateAsync(new ControlCriterionMappingLedger(tenantId,
                programId), ct).ConfigureAwait(false);
            foreach (var mapping in mappings.ReadAll().Where(static mapping =>
                         mapping.Status == "pending" && mapping.Versions.Count > 0 &&
                         mapping.Versions[^1].Status == "proposed"))
            {
                var proposal = mapping.Versions[^1];
                var proposerMemberId = Uuid.Parse(proposal.ProposedBy.Id,
                    CultureInfo.InvariantCulture);
                var candidate = ControlCriterionMappingReviewCandidate(tenantId, programId,
                    mapping.ControlId, mapping.MappingId, mapping.CriterionIdentifier,
                    proposerMemberId, proposal.VersionNumber, proposal.ProposedAt);
                if (workItemId is { } wantedMapping && candidate.WorkItemId != wantedMapping)
                    continue;
                candidates.Add(candidate);
            }
        }
        if (!projectedKinds.Contains(CriterionApplicabilityReview))
        {
            var applicability = await reader.HydrateAsync(new CriterionApplicabilityLedger(tenantId,
                programId), ct).ConfigureAwait(false);
            foreach (var decision in applicability.ReadAll().Where(static decision =>
                         decision.Status == "pending" && decision.Versions.Count > 0 &&
                         decision.Versions[^1].Status == "proposed"))
            {
                var proposal = decision.Versions[^1];
                var proposerMemberId = Uuid.Parse(proposal.ProposedBy.Id,
                    CultureInfo.InvariantCulture);
                var candidate = CriterionApplicabilityReviewCandidate(tenantId, programId,
                    decision.DecisionId, decision.CriterionIdentifier,
                    proposerMemberId, proposal.VersionNumber, proposal.ProposedAt);
                if (workItemId is { } wantedApplicability &&
                    candidate.WorkItemId != wantedApplicability)
                    continue;
                candidates.Add(candidate);
            }
        }
        if (!projectedKinds.Contains(ControlOperatingPlanApproval))
        {
            operations ??= await reader.HydrateAsync(new ControlOperationsLedger(tenantId,
                programId), ct).ConfigureAwait(false);
            foreach (var controlId in operations.PlannedControlIds)
            {
                if (operations.PendingPlan(controlId) is not { } pending ||
                    workItemId is { } wantedPlan && WorkCandidate.IdFor(pending.PlanVersionId,
                        ControlOperatingPlanApproval) != wantedPlan)
                    continue;
                var control = await ControlOperationsSource.LoadControlAsync(reader, tenantId,
                    programId, controlId, ct).ConfigureAwait(false);
                if (control is null || control.IsRetired ||
                    control.ApprovedVersion is not
                    { Status: ControlOperationsLedger.Approved } current ||
                    current.VersionId != pending.ControlVersionId)
                    continue;
                candidates.Add(ControlOperatingPlanApprovalCandidate(tenantId, programId,
                    controlId, current.Identifier, pending.PlanVersionId,
                    pending.ProposerMemberId, pending.ProposedAt));
            }
        }
        if (boundaries is not null &&
            BoundaryDecisionWork.ProjectedKinds.Any(kind => !projectedKinds.Contains(kind)))
        {
            var boundaryWork = await BoundaryDecisionWork.LoadAsync(reader, boundaries,
                tenantId, programId, now, workItemId, ct).ConfigureAwait(false);
            if (!boundaryWork.IsSuccess)
                return Result<IReadOnlyList<WorkCandidate>>.Failure(boundaryWork.Error);
            candidates.AddRange(boundaryWork.Value.Where(candidate =>
                !projectedKinds.Contains(candidate.Kind)));
        }
        return Result<IReadOnlyList<WorkCandidate>>.Success(candidates);
    }

    public static Uuid RiskTreatmentWorkItemId(Uuid programId, Uuid riskId, Uuid sourceId,
        string kind) => WorkCandidate.IdFor(Uuid.CreateVersion5(programId,
        $"risk-treatment-work\n{riskId}\n{sourceId}"), kind);

    public static WorkCandidate ControlOccurrenceCandidate(Uuid tenantId, Uuid programId,
        Uuid controlId, string identifier, Uuid occurrenceId, DateOnly? periodStart,
        DateOnly? dueOn, OperatingHolder responsible, OperatingHolder? backup)
    {
        var period = periodStart is { } start
            ? $"{start:yyyy-MM-dd}"
            : occurrenceId.ToString();
        var created = periodStart is { } from
            ? new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
            : DateTimeOffset.MinValue;
        var path = $"/api/v1/tenants/{tenantId}/programs/{programId}/controls/{controlId}" +
                   $"/occurrences/{occurrenceId}";
        return new WorkCandidate(WorkCandidate.IdFor(occurrenceId, ControlOccurrence),
            ControlOccurrence, occurrenceId, controlId, null, $"Perform {identifier} for {period}",
            "The approved operating plan expects this control to operate for the period.", dueOn,
            null, "attest", path + "/attestations", responsible, backup, new HashSet<Uuid>(),
            created);
    }

    public static WorkCandidate OccurrenceReviewCandidate(Uuid tenantId, Uuid programId,
        Uuid controlId, string identifier, Uuid occurrenceId, DateOnly? periodStart,
        DateOnly? dueOn, Uuid reviewerMemberId, OperatingHolder performedBy,
        Uuid recorderMemberId, DateTimeOffset recordedAt)
    {
        var period = periodStart is { } start
            ? $"{start:yyyy-MM-dd}"
            : occurrenceId.ToString();
        var path = $"/api/v1/tenants/{tenantId}/programs/{programId}/controls/{controlId}" +
                   $"/occurrences/{occurrenceId}";
        var excluded = new HashSet<Uuid> { recorderMemberId };
        if (performedBy.Kind == OperatingAuthority.MemberHolder)
            excluded.Add(performedBy.Id);
        return new WorkCandidate(WorkCandidate.IdFor(occurrenceId, OccurrenceReview),
            OccurrenceReview, occurrenceId, controlId, null, $"Review {identifier} for {period}",
            "An attestation awaits independent review by someone who did not perform or record it.",
            dueOn, null, "review", path + "/reviews",
            new OperatingHolder(OperatingAuthority.MemberHolder, reviewerMemberId), null, excluded,
            recordedAt);
    }

    public static WorkCandidate ControlOperatingPlanApprovalCandidate(Uuid tenantId,
        Uuid programId, Uuid controlId, string controlIdentifier, Uuid planVersionId,
        Uuid proposerMemberId, DateTimeOffset proposedAt) => new(
        WorkCandidate.IdFor(planVersionId, ControlOperatingPlanApproval),
        ControlOperatingPlanApproval, planVersionId, controlId, null,
        $"Approve operating plan for {controlIdentifier}",
        "A control operating plan is awaiting independent approval.", null, null, "approve",
        $"/api/v1/tenants/{tenantId}/programs/{programId}/controls/{controlId}/" +
        $"operating-plan/proposals/{planVersionId}/approvals",
        new OperatingHolder(OperatingAuthority.ProgramReviewerHolder, programId), null,
        new HashSet<Uuid> { proposerMemberId }, proposedAt);

    public static WorkCandidate ControlEvaluationReviewCandidate(Uuid tenantId, Uuid programId,
        Uuid controlId, Uuid evaluationId, Uuid evaluatorMemberId, int round,
        DateTimeOffset submittedAt) => new(ControlEvaluationReviewWorkItemId(evaluationId, round),
        ControlEvaluationReview, evaluationId, controlId, null, "Review control evaluation",
        $"Control evaluation round {round} is awaiting independent review.", null, null, "review",
        $"/api/v1/tenants/{tenantId}/programs/{programId}/controls/{controlId}/" +
        $"evaluations/{evaluationId}/reviews",
        new OperatingHolder(OperatingAuthority.ProgramReviewerHolder, programId), null,
        new HashSet<Uuid> { evaluatorMemberId }, submittedAt);

    public static Uuid ControlEvaluationReviewWorkItemId(Uuid evaluationId, int round)
    {
        var identity = Uuid.CreateVersion5(evaluationId, $"control-evaluation-review\n{round}");
        return WorkCandidate.IdFor(identity, ControlEvaluationReview);
    }

    public static WorkCandidate ControlCriterionMappingReviewCandidate(Uuid tenantId,
        Uuid programId, Uuid controlId, Uuid mappingId, string criterionIdentifier,
        Uuid proposerMemberId, int versionNumber, DateTimeOffset proposedAt) =>
        new(ControlCriterionMappingReviewWorkItemId(mappingId, versionNumber),
            ControlCriterionMappingReview, mappingId, controlId, null,
            $"Review control mapping for {criterionIdentifier}",
            "A control-to-criteria mapping proposal is awaiting independent review.", null, null,
            "review", $"/api/v1/tenants/{tenantId}/programs/{programId}/" +
            $"control-mappings/{mappingId}/reviews",
            new OperatingHolder(OperatingAuthority.ProgramReviewerHolder, programId), null,
            new HashSet<Uuid> { proposerMemberId }, proposedAt);

    public static Uuid ControlCriterionMappingReviewWorkItemId(Uuid mappingId, int versionNumber)
    {
        var identity = Uuid.CreateVersion5(mappingId,
            $"control-criterion-mapping-review\n{versionNumber}");
        return WorkCandidate.IdFor(identity, ControlCriterionMappingReview);
    }

    public static WorkCandidate CriterionApplicabilityReviewCandidate(Uuid tenantId,
        Uuid programId, Uuid decisionId, string criterionIdentifier,
        Uuid proposerMemberId, int versionNumber, DateTimeOffset proposedAt) =>
        new(CriterionApplicabilityReviewWorkItemId(decisionId, versionNumber),
            CriterionApplicabilityReview, decisionId, null, null,
            $"Review not-applicable proposal for {criterionIdentifier}",
            "A criterion not-applicable proposal is awaiting independent review.", null, null,
            "review", $"/api/v1/tenants/{tenantId}/programs/{programId}/" +
            $"criterion-applicability/{decisionId}/reviews",
            new OperatingHolder(OperatingAuthority.ProgramReviewerHolder, programId), null,
            new HashSet<Uuid> { proposerMemberId }, proposedAt);

    public static Uuid CriterionApplicabilityReviewWorkItemId(Uuid decisionId, int versionNumber)
    {
        var identity = Uuid.CreateVersion5(decisionId,
            $"criterion-applicability-review\n{versionNumber}");
        return WorkCandidate.IdFor(identity, CriterionApplicabilityReview);
    }
}

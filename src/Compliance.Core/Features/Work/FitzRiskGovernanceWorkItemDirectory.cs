using System.Globalization;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Risks;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

sealed class FitzRiskGovernanceWorkItemDirectory(IKvClient client)
    : FitzKvProjectionStore(client,
        "kv://bdgrz/accountable-work-items/risk-governance-v1", ProjectorName),
      IAccountableWorkItemDirectoryReader, IRiskGovernanceWorkItemProjection
{
    public const string ProjectorName = "AccountableWorkItemRiskGovernanceV1";
    const string RevisionKey = ProjectorName;

    string IAccountableWorkItemDirectoryReader.ProjectorName => ProjectorName;

    public IReadOnlyCollection<string> ProjectedKinds =>
    [
        WorkSource.RiskTreatmentAction,
        WorkSource.RiskTreatmentActionReview,
        WorkSource.RiskControlTreatmentReview,
    ];

    public EventStreamPattern SourcePattern(Uuid tenantId) =>
        EventStreamPattern.ForPattern(tenantId.ToString(), "risk-governance");

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) => base.LoadCheckpointAsync(new CheckpointIdentity(
        ProjectorName, SourcePattern(tenantId)), ct);

    public async ValueTask<long> LoadRevisionAsync(Uuid tenantId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return (await RiskGovernanceWorkItemDirectorySchema.Revisions.GetAsync(tx, RevisionKey,
            ct).ConfigureAwait(false))?.Revision ?? 0;
    }

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        var scope = ScopeFor(domainEvent);
        await AdvanceRiskRevisionAsync(scope.TenantId, scope.ProgramId, scope.RiskId,
            scope.Revision, ct).ConfigureAwait(false);

        switch (domainEvent)
        {
            case RiskOwnerAssigned:
            case RiskReassessmentTriggered:
                break;
            case RiskControlTreatmentProposed proposed:
                await ProjectControlTreatmentAsync(proposed, ct).ConfigureAwait(false);
                break;
            case RiskControlTreatmentReviewed controlReviewed:
                await RemoveControlTreatmentAsync(controlReviewed, required: true, ct)
                    .ConfigureAwait(false);
                break;
            case RiskControlTreatmentRetired retired:
                await RemoveControlTreatmentAsync(retired, required: false, ct)
                    .ConfigureAwait(false);
                break;
            case RiskTreatmentActionAdded added:
                await AddTreatmentActionAsync(added, ct).ConfigureAwait(false);
                break;
            case RiskTreatmentActionRevised revised:
                await ReviseTreatmentActionAsync(revised, ct).ConfigureAwait(false);
                break;
            case RiskTreatmentActionCancelled cancelled:
                await CancelTreatmentActionAsync(cancelled, ct).ConfigureAwait(false);
                break;
            case RiskTreatmentActionCompletionSubmitted submitted:
                await SubmitTreatmentActionCompletionAsync(submitted, ct).ConfigureAwait(false);
                break;
            case RiskTreatmentActionCompletionReviewed completionReviewed:
                await ReviewTreatmentActionCompletionAsync(completionReviewed, ct)
                    .ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException(
                    $"The risk governance projector cannot apply {domainEvent.GetType().Name}.");
        }

        await IncrementRevisionAsync(ct).ConfigureAwait(false);
    }

    public async ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId,
        Uuid programId, CancellationToken ct = default)
    {
        var candidates = new List<WorkCandidate>();
        string? cursor = null;
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        do
        {
            var page = await RiskGovernanceWorkItemDirectorySchema.WorkItems.QueryAsync(tx,
                RiskGovernanceWorkItemDirectorySchema.ByProgram.Query()
                    .WithPrefix(programId.ToString()).Take(200).After(cursor), ct)
                .ConfigureAwait(false);
            foreach (var item in page.Items)
            {
                if (item.TenantId != tenantId || item.ProgramId != programId ||
                    item.RiskId is not { } riskId || !IsRiskWorkItem(item) ||
                    item.WorkItemId != WorkItemIdFor(programId, item.Kind, item.SourceId,
                        riskId))
                    return Result<IReadOnlyList<WorkCandidate>>.Failure(InvalidScope());
                candidates.Add(item.ToCandidate());
            }
            cursor = page.NextCursor;
        } while (cursor is not null);

        return Result<IReadOnlyList<WorkCandidate>>.Success(candidates);
    }

    async ValueTask ProjectControlTreatmentAsync(RiskControlTreatmentProposed proposed,
        CancellationToken ct)
    {
        var treatment = proposed.Treatment;
        if (treatment.RiskId != proposed.RiskId || treatment.Status != "proposed")
            throw new InvalidOperationException(
                "Only the proposed control treatment for the event risk can enter the work projection.");
        var proposerId = Uuid.Parse(treatment.ProposedBy.Id, CultureInfo.InvariantCulture);
        if (proposerId != proposed.ProposerMemberId)
            throw new InvalidOperationException(
                "A control treatment proposal must preserve its recorded proposer.");
        var candidate = ControlTreatmentCandidate(proposed.TenantId, proposed.ProgramId,
            treatment, proposerId);
        await AddWorkItemAsync(proposed.TenantId, proposed.ProgramId, proposed.RiskId,
            candidate, ct).ConfigureAwait(false);
    }

    async ValueTask RemoveControlTreatmentAsync(RiskControlTreatmentReviewed reviewed,
        bool required, CancellationToken ct)
    {
        var identity = ControlTreatmentIdentity(reviewed.RiskId, reviewed.TreatmentId);
        await RemoveWorkItemAsync(reviewed.TenantId, reviewed.ProgramId, reviewed.RiskId,
            WorkCandidate.IdFor(identity, WorkSource.RiskControlTreatmentReview),
            WorkSource.RiskControlTreatmentReview, reviewed.TreatmentId, required, ct)
            .ConfigureAwait(false);
    }

    async ValueTask RemoveControlTreatmentAsync(RiskControlTreatmentRetired retired,
        bool required, CancellationToken ct)
    {
        var identity = ControlTreatmentIdentity(retired.RiskId, retired.TreatmentId);
        await RemoveWorkItemAsync(retired.TenantId, retired.ProgramId, retired.RiskId,
            WorkCandidate.IdFor(identity, WorkSource.RiskControlTreatmentReview),
            WorkSource.RiskControlTreatmentReview, retired.TreatmentId, required, ct)
            .ConfigureAwait(false);
    }

    async ValueTask AddTreatmentActionAsync(RiskTreatmentActionAdded added,
        CancellationToken ct)
    {
        if (added.Action.RiskId != added.RiskId ||
            added.Action.Status != RiskGovernanceLedger.ActionOpen)
            throw new InvalidOperationException(
                "Only an open treatment action for the event risk can enter the work projection.");
        var state = new RiskTreatmentActionWorkState(added.TenantId, added.ProgramId,
            added.RiskId, added.Action, null);
        var existing = await RiskGovernanceWorkItemDirectorySchema.TreatmentActions.GetAsync(
            Transaction, RiskGovernanceWorkItemDirectorySchema.ActionKey(added.ProgramId,
                added.RiskId, added.Action.ActionId), ct).ConfigureAwait(false);
        if (existing is not null)
            throw new InvalidOperationException(
                "A treatment action cannot be added twice to the work projection.");
        await RiskGovernanceWorkItemDirectorySchema.TreatmentActions.InsertAsync(Transaction,
            state, ct).ConfigureAwait(false);
        await AddWorkItemAsync(added.TenantId, added.ProgramId, added.RiskId,
            TreatmentActionCandidate(added.TenantId, added.ProgramId, state), ct)
            .ConfigureAwait(false);
    }

    async ValueTask ReviseTreatmentActionAsync(RiskTreatmentActionRevised revised,
        CancellationToken ct)
    {
        var current = await LoadTreatmentActionAsync(revised.TenantId, revised.ProgramId,
            revised.RiskId, revised.ActionId, ct).ConfigureAwait(false);
        if (current.Action.Status != RiskGovernanceLedger.ActionOpen)
            throw new InvalidOperationException(
                "Only an open treatment action can be revised in the work projection.");
        var action = current.Action with
        {
            Title = revised.Title,
            TargetState = revised.TargetState,
            ExpectedEvidence = revised.ExpectedEvidence,
            DueOn = revised.DueOn,
            AccountableMemberId = revised.AccountableMemberId,
            EvidenceRequestIds = revised.EvidenceRequestIds,
        };
        var updated = current with { Action = action };
        await RiskGovernanceWorkItemDirectorySchema.TreatmentActions.ReplaceAsync(Transaction,
            current, updated, ct).ConfigureAwait(false);
        await AddWorkItemAsync(revised.TenantId, revised.ProgramId, revised.RiskId,
            TreatmentActionCandidate(revised.TenantId, revised.ProgramId, updated), ct)
            .ConfigureAwait(false);
    }

    async ValueTask CancelTreatmentActionAsync(RiskTreatmentActionCancelled cancelled,
        CancellationToken ct)
    {
        var state = await LoadTreatmentActionAsync(cancelled.TenantId, cancelled.ProgramId,
            cancelled.RiskId, cancelled.ActionId, ct).ConfigureAwait(false);
        if (state.Action.Status == RiskGovernanceLedger.ActionOpen)
            await RemoveWorkItemAsync(cancelled.TenantId, cancelled.ProgramId,
                cancelled.RiskId,
                WorkSource.RiskTreatmentWorkItemId(cancelled.ProgramId, cancelled.RiskId,
                    cancelled.ActionId, WorkSource.RiskTreatmentAction),
                WorkSource.RiskTreatmentAction, cancelled.ActionId, required: true, ct)
                .ConfigureAwait(false);
        else if (state.Action.Status == RiskGovernanceLedger.ActionSubmitted)
        {
            var submission = PendingCompletion(state).SubmissionId;
            await RemoveWorkItemAsync(cancelled.TenantId, cancelled.ProgramId,
                cancelled.RiskId,
                WorkSource.RiskTreatmentWorkItemId(cancelled.ProgramId, cancelled.RiskId,
                    submission, WorkSource.RiskTreatmentActionReview),
                WorkSource.RiskTreatmentActionReview, submission, required: true, ct)
                .ConfigureAwait(false);
        }
        else
            throw new InvalidOperationException(
                "Only unfinished treatment action work can be cancelled.");

        await RiskGovernanceWorkItemDirectorySchema.TreatmentActions.DeleteAsync(Transaction,
            state, ct).ConfigureAwait(false);
    }

    async ValueTask SubmitTreatmentActionCompletionAsync(
        RiskTreatmentActionCompletionSubmitted submitted, CancellationToken ct)
    {
        var current = await LoadTreatmentActionAsync(submitted.TenantId, submitted.ProgramId,
            submitted.RiskId, submitted.ActionId, ct).ConfigureAwait(false);
        if (current.Action.Status != RiskGovernanceLedger.ActionOpen)
            throw new InvalidOperationException(
                "Only an open treatment action can submit completion work.");
        var completion = new RiskTreatmentActionCompletionView(submitted.SubmissionId,
            submitted.Summary, submitted.EvidenceRequestIds, submitted.SubmittedBy,
            submitted.SubmittedAt, null, null, null, null, null, null);
        var updated = current with
        {
            Action = current.Action with
            {
                Status = RiskGovernanceLedger.ActionSubmitted,
                Completions = [.. current.Action.Completions, completion],
            },
            PendingSubmitterMemberId = submitted.SubmitterMemberId,
        };
        await RiskGovernanceWorkItemDirectorySchema.TreatmentActions.ReplaceAsync(Transaction,
            current, updated, ct).ConfigureAwait(false);
        await RemoveWorkItemAsync(submitted.TenantId, submitted.ProgramId, submitted.RiskId,
            WorkSource.RiskTreatmentWorkItemId(submitted.ProgramId, submitted.RiskId,
                submitted.ActionId, WorkSource.RiskTreatmentAction),
            WorkSource.RiskTreatmentAction, submitted.ActionId, required: true, ct)
            .ConfigureAwait(false);
        await AddWorkItemAsync(submitted.TenantId, submitted.ProgramId, submitted.RiskId,
            TreatmentActionReviewCandidate(submitted.TenantId, submitted.ProgramId, updated), ct)
            .ConfigureAwait(false);
    }

    async ValueTask ReviewTreatmentActionCompletionAsync(
        RiskTreatmentActionCompletionReviewed reviewed, CancellationToken ct)
    {
        var current = await LoadTreatmentActionAsync(reviewed.TenantId, reviewed.ProgramId,
            reviewed.RiskId, reviewed.ActionId, ct).ConfigureAwait(false);
        if (current.Action.Status != RiskGovernanceLedger.ActionSubmitted ||
            reviewed.Outcome is not (RiskGovernanceLedger.Accept or RiskGovernanceLedger.Reject))
            throw new InvalidOperationException(
                "Only a submitted treatment completion with a valid outcome can be reviewed.");
        var pending = PendingCompletion(current);
        var reviewWorkItemId = WorkSource.RiskTreatmentWorkItemId(reviewed.ProgramId,
            reviewed.RiskId, pending.SubmissionId, WorkSource.RiskTreatmentActionReview);
        await RemoveWorkItemAsync(reviewed.TenantId, reviewed.ProgramId, reviewed.RiskId,
            reviewWorkItemId, WorkSource.RiskTreatmentActionReview, pending.SubmissionId,
            required: true, ct).ConfigureAwait(false);
        var completions = current.Action.Completions.ToArray();
        var completionIndex = Array.FindLastIndex(completions,
            static completion => completion.ReviewOutcome is null);
        completions[completionIndex] = pending with
        {
            ReviewDecisionId = reviewed.DecisionId,
            ReviewOutcome = reviewed.Outcome,
            ReviewedBy = reviewed.Reviewer,
            ReviewRationale = reviewed.Rationale,
            ReviewedAt = reviewed.ReviewedAt,
            SeparationOfDutiesWaiverId = reviewed.SeparationOfDutiesWaiverId,
        };
        var action = current.Action with
        {
            Status = reviewed.Outcome == RiskGovernanceLedger.Accept
                ? RiskGovernanceLedger.ActionCompleted
                : RiskGovernanceLedger.ActionOpen,
            Completions = completions,
        };
        var updated = current with { Action = action, PendingSubmitterMemberId = null };
        if (action.Status == RiskGovernanceLedger.ActionCompleted)
        {
            await RiskGovernanceWorkItemDirectorySchema.TreatmentActions.DeleteAsync(Transaction,
                current, ct).ConfigureAwait(false);
            return;
        }

        await RiskGovernanceWorkItemDirectorySchema.TreatmentActions.ReplaceAsync(Transaction,
            current, updated, ct).ConfigureAwait(false);
        await AddWorkItemAsync(reviewed.TenantId, reviewed.ProgramId, reviewed.RiskId,
            TreatmentActionCandidate(reviewed.TenantId, reviewed.ProgramId, updated), ct)
            .ConfigureAwait(false);
    }

    async ValueTask AddWorkItemAsync(Uuid tenantId, Uuid programId, Uuid riskId,
        WorkCandidate candidate, CancellationToken ct)
    {
        var workItem = AccountableWorkItemView.FromCandidate(tenantId, programId, candidate,
            riskId);
        var existing = await RiskGovernanceWorkItemDirectorySchema.WorkItems.GetAsync(
            Transaction, workItem.WorkItemId, ct).ConfigureAwait(false);
        if (existing is null)
            await RiskGovernanceWorkItemDirectorySchema.WorkItems.InsertAsync(Transaction,
                workItem, ct).ConfigureAwait(false);
        else if (existing.TenantId != tenantId || existing.ProgramId != programId ||
                 existing.RiskId != riskId || existing.WorkItemId != candidate.WorkItemId ||
                 existing.Kind != candidate.Kind || existing.SourceId != candidate.SourceId)
            throw new InvalidOperationException(
                "Risk work cannot change tenant, program, risk, or source identity.");
        else
            await RiskGovernanceWorkItemDirectorySchema.WorkItems.ReplaceAsync(Transaction,
                existing, workItem, ct).ConfigureAwait(false);
    }

    async ValueTask RemoveWorkItemAsync(Uuid tenantId, Uuid programId, Uuid riskId,
        Uuid workItemId, string kind, Uuid sourceId, bool required, CancellationToken ct)
    {
        var existing = await RiskGovernanceWorkItemDirectorySchema.WorkItems.GetAsync(
            Transaction, workItemId, ct).ConfigureAwait(false);
        if (existing is null)
        {
            if (required)
                throw new InvalidOperationException(
                    "Risk work cannot be removed before its source event is projected.");
            return;
        }
        ValidateWorkItem(existing, tenantId, programId, riskId, kind, sourceId);
        await RiskGovernanceWorkItemDirectorySchema.WorkItems.DeleteAsync(Transaction, existing,
            ct).ConfigureAwait(false);
    }

    async ValueTask<RiskTreatmentActionWorkState> LoadTreatmentActionAsync(Uuid tenantId,
        Uuid programId, Uuid riskId, Uuid actionId, CancellationToken ct)
    {
        var state = await RiskGovernanceWorkItemDirectorySchema.TreatmentActions.GetAsync(
            Transaction, RiskGovernanceWorkItemDirectorySchema.ActionKey(programId, riskId,
                actionId), ct).ConfigureAwait(false) ?? throw new InvalidOperationException(
            "A treatment action event cannot be projected before its added event.");
        if (state.TenantId != tenantId || state.ProgramId != programId || state.RiskId != riskId ||
            state.Action.ActionId != actionId || state.Action.RiskId != riskId)
            throw new InvalidOperationException(
                "A treatment action event cannot change tenant, program, or risk scope.");
        return state;
    }

    async ValueTask AdvanceRiskRevisionAsync(Uuid tenantId, Uuid programId, Uuid riskId,
        long revision, CancellationToken ct)
    {
        var key = RiskGovernanceWorkItemDirectorySchema.RiskKey(programId, riskId);
        var current = await RiskGovernanceWorkItemDirectorySchema.RiskRevisions.GetAsync(
            Transaction, key, ct).ConfigureAwait(false);
        if (current is null)
        {
            if (revision != 1)
                throw new InvalidOperationException(
                    "A risk work projection cannot skip the first source revision.");
            await RiskGovernanceWorkItemDirectorySchema.RiskRevisions.InsertAsync(Transaction,
                new RiskGovernanceWorkRevision(tenantId, programId, riskId, revision), ct)
                .ConfigureAwait(false);
            return;
        }
        if (current.TenantId != tenantId || current.ProgramId != programId ||
            current.RiskId != riskId || revision != current.Revision + 1)
            throw new InvalidOperationException(
                "A risk event cannot change scope or skip a source revision in the work projection.");
        await RiskGovernanceWorkItemDirectorySchema.RiskRevisions.ReplaceAsync(Transaction,
            current, current with { Revision = revision }, ct).ConfigureAwait(false);
    }

    async ValueTask IncrementRevisionAsync(CancellationToken ct)
    {
        var current = await RiskGovernanceWorkItemDirectorySchema.Revisions.GetAsync(
            Transaction, RevisionKey, ct).ConfigureAwait(false);
        var next = new AccountableWorkItemProjectionRevision(ProjectorName,
            checked((current?.Revision ?? 0) + 1));
        if (current is null)
            await RiskGovernanceWorkItemDirectorySchema.Revisions.InsertAsync(Transaction, next, ct)
                .ConfigureAwait(false);
        else
            await RiskGovernanceWorkItemDirectorySchema.Revisions.ReplaceAsync(Transaction,
                current, next, ct).ConfigureAwait(false);
    }

    static WorkCandidate TreatmentActionCandidate(Uuid tenantId, Uuid programId,
        RiskTreatmentActionWorkState state)
    {
        var action = state.Action;
        return new WorkCandidate(
            WorkSource.RiskTreatmentWorkItemId(programId, state.RiskId, action.ActionId,
                WorkSource.RiskTreatmentAction),
            WorkSource.RiskTreatmentAction, action.ActionId, null, null, action.Title,
            $"Treatment work for a risk. Target state: {action.TargetState}", action.DueOn,
            null, "complete",
            $"/api/v1/tenants/{tenantId}/programs/{programId}/risks/{state.RiskId}/" +
            $"treatment-actions/{action.ActionId}/completions",
            new OperatingHolder(OperatingAuthority.MemberHolder, action.AccountableMemberId),
            null, new HashSet<Uuid>(), action.CreatedAt);
    }

    static WorkCandidate TreatmentActionReviewCandidate(Uuid tenantId, Uuid programId,
        RiskTreatmentActionWorkState state)
    {
        var completion = PendingCompletion(state);
        var submitter = state.PendingSubmitterMemberId ?? throw new InvalidOperationException(
            "A submitted risk action completion is missing its submitter.");
        var excluded = new HashSet<Uuid>
        {
            state.Action.AccountableMemberId,
            submitter,
        };
        return new WorkCandidate(
            WorkSource.RiskTreatmentWorkItemId(programId, state.RiskId,
                completion.SubmissionId, WorkSource.RiskTreatmentActionReview),
            WorkSource.RiskTreatmentActionReview, completion.SubmissionId, null, null,
            $"Review completion for {state.Action.Title}",
            "A risk treatment action completion is awaiting independent review.",
            state.Action.DueOn, null, "review",
            $"/api/v1/tenants/{tenantId}/programs/{programId}/risks/{state.RiskId}/" +
            $"treatment-actions/{state.Action.ActionId}/completion-reviews",
            new OperatingHolder(OperatingAuthority.ProgramManagerHolder, programId), null,
            excluded, completion.SubmittedAt);
    }

    static WorkCandidate ControlTreatmentCandidate(Uuid tenantId, Uuid programId,
        RiskControlTreatmentView treatment, Uuid proposerId)
    {
        var identity = ControlTreatmentIdentity(treatment.RiskId, treatment.TreatmentId);
        return new WorkCandidate(WorkCandidate.IdFor(identity,
                WorkSource.RiskControlTreatmentReview),
            WorkSource.RiskControlTreatmentReview, treatment.TreatmentId, treatment.ControlId,
            null, $"Review control treatment for risk {treatment.RiskId}",
            "A risk control-treatment assertion is awaiting independent review.", null, null,
            "review",
            $"/api/v1/tenants/{tenantId}/programs/{programId}/risks/{treatment.RiskId}/" +
            $"control-treatments/{treatment.TreatmentId}/reviews",
            new OperatingHolder(OperatingAuthority.ProgramManagerHolder, programId), null,
            new HashSet<Uuid> { proposerId }, treatment.ProposedAt);
    }

    static RiskTreatmentActionCompletionView PendingCompletion(
        RiskTreatmentActionWorkState state) =>
        state.Action.Completions.LastOrDefault(static completion =>
            completion.ReviewOutcome is null) ?? throw new InvalidOperationException(
            "A submitted treatment action is missing its pending completion.");

    static Uuid ControlTreatmentIdentity(Uuid riskId, Uuid treatmentId) =>
        Uuid.CreateVersion5(riskId, $"risk-control-treatment-review\n{treatmentId}");

    static (Uuid TenantId, Uuid ProgramId, Uuid RiskId, long Revision) ScopeFor(
        DomainEvent domainEvent) => domainEvent switch
        {
            RiskOwnerAssigned ev => (ev.TenantId, ev.ProgramId, ev.RiskId, ev.Revision),
            RiskControlTreatmentProposed ev => (ev.TenantId, ev.ProgramId, ev.RiskId, ev.Revision),
            RiskControlTreatmentReviewed ev => (ev.TenantId, ev.ProgramId, ev.RiskId, ev.Revision),
            RiskControlTreatmentRetired ev => (ev.TenantId, ev.ProgramId, ev.RiskId, ev.Revision),
            RiskReassessmentTriggered ev => (ev.TenantId, ev.ProgramId, ev.RiskId, ev.Revision),
            RiskTreatmentActionAdded ev => (ev.TenantId, ev.ProgramId, ev.RiskId, ev.Revision),
            RiskTreatmentActionCancelled ev => (ev.TenantId, ev.ProgramId, ev.RiskId, ev.Revision),
            RiskTreatmentActionRevised ev => (ev.TenantId, ev.ProgramId, ev.RiskId, ev.Revision),
            RiskTreatmentActionCompletionSubmitted ev =>
                (ev.TenantId, ev.ProgramId, ev.RiskId, ev.Revision),
            RiskTreatmentActionCompletionReviewed ev =>
                (ev.TenantId, ev.ProgramId, ev.RiskId, ev.Revision),
            _ => throw new InvalidOperationException(
                $"The risk governance projector cannot apply {domainEvent.GetType().Name}.")
        };

    static bool IsRiskWorkItem(AccountableWorkItemView item) => item.Kind is
        WorkSource.RiskTreatmentAction or WorkSource.RiskTreatmentActionReview or
        WorkSource.RiskControlTreatmentReview;

    static Uuid WorkItemIdFor(Uuid programId, string kind, Uuid sourceId, Uuid riskId) => kind switch
    {
        WorkSource.RiskTreatmentAction or WorkSource.RiskTreatmentActionReview =>
            WorkSource.RiskTreatmentWorkItemId(programId, riskId, sourceId, kind),
        WorkSource.RiskControlTreatmentReview => WorkCandidate.IdFor(
            ControlTreatmentIdentity(riskId, sourceId), kind),
        _ => Uuid.Empty,
    };

    static void ValidateWorkItem(AccountableWorkItemView item, Uuid tenantId, Uuid programId,
        Uuid riskId, string kind, Uuid sourceId)
    {
        if (item.TenantId != tenantId || item.ProgramId != programId || item.RiskId != riskId ||
            item.Kind != kind || item.SourceId != sourceId ||
            item.WorkItemId != WorkItemIdFor(programId, kind, sourceId, riskId))
            throw new InvalidOperationException(
                "Risk work cannot change tenant, program, risk, or source scope.");
    }

    static RequestError InvalidScope() => new(RequestErrorKind.Conflict,
        "The risk work projection has an invalid tenant, program, risk, or source scope.");
}

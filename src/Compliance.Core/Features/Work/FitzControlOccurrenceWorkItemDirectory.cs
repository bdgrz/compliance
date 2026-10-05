using Bdgrz.Compliance.Features.Operations;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

sealed class FitzControlOccurrenceWorkItemDirectory(IKvClient client, IAggregateReader sourceReader)
    : FitzKvProjectionStore(client,
        "kv://bdgrz/accountable-work-items/control-occurrences-v1", ProjectorName),
      IAccountableWorkItemDirectoryReader, IControlOccurrenceWorkItemProjection
{
    public const string ProjectorName = "AccountableWorkItemControlOccurrencesV1";
    const string RevisionKey = ProjectorName;
    const string PendingApproval = "pending_approval";
    const string Approved = "approved";
    const string Superseded = "superseded";
    const string Open = "open";
    const string Submitted = "submitted";
    const string Deferred = "deferred";
    const string Returned = "returned";

    string IAccountableWorkItemDirectoryReader.ProjectorName => ProjectorName;

    public IReadOnlyCollection<string> ProjectedKinds =>
        [WorkSource.ControlOccurrence, WorkSource.OccurrenceReview];

    public EventStreamPattern SourcePattern(Uuid tenantId) =>
        EventStreamPattern.ForPattern(tenantId.ToString(), "control-operations");

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) => base.LoadCheckpointAsync(new CheckpointIdentity(
        ProjectorName, SourcePattern(tenantId)), ct);

    public async ValueTask<long> LoadRevisionAsync(Uuid tenantId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return (await ControlOccurrenceWorkItemDirectorySchema.Revisions.GetAsync(tx,
            RevisionKey, ct).ConfigureAwait(false))?.Revision ?? 0;
    }

    public ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId,
        Uuid programId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return LoadProgramAsync(tenantId, programId, today,
            today.AddDays(ControlCadenceSchedule.MaximumDueWithinDays), null, ct);
    }

    public async ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId,
        Uuid programId, DateOnly today, DateOnly horizon, Uuid? workItemId,
        CancellationToken ct = default)
    {
        var candidates = new List<WorkCandidate>();
        var plansByControl = new Dictionary<Uuid, Dictionary<Uuid, ControlOccurrencePlanWorkState>>();
        var linesByControl = new Dictionary<Uuid, ControlOccurrenceLineWorkState>();
        string? cursor = null;
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        do
        {
            var page = await ControlOccurrenceWorkItemDirectorySchema.Plans.QueryAsync(tx,
                ControlOccurrenceWorkItemDirectorySchema.PlansByProgram.Query()
                    .WithPrefix(programId.ToString()).Take(200).After(cursor), ct)
                .ConfigureAwait(false);
            foreach (var plan in page.Items)
            {
                if (plan.TenantId != tenantId || plan.ProgramId != programId ||
                    plan.ControlId == Uuid.Empty || plan.PlanVersionId == Uuid.Empty ||
                    plan.ControlVersionId == Uuid.Empty || plan.Revision < 1 ||
                    plan.ReviewerMemberId == Uuid.Empty || !IsHolder(plan.Owner) ||
                    plan.BackupOwner is not null && !IsHolder(plan.BackupOwner) ||
                    plan.Status is not (PendingApproval or Approved or Superseded) ||
                    ControlCadenceSchedule.Validate(plan.Cadence) is not null)
                    return Result<IReadOnlyList<WorkCandidate>>.Failure(InvalidScope());

                if (!plansByControl.TryGetValue(plan.ControlId, out var controlPlans))
                    plansByControl[plan.ControlId] = controlPlans = [];
                if (!controlPlans.TryAdd(plan.PlanVersionId, plan))
                    return Result<IReadOnlyList<WorkCandidate>>.Failure(InvalidScope());

                if (!linesByControl.TryGetValue(plan.ControlId, out var line))
                {
                    line = await ControlOccurrenceWorkItemDirectorySchema.Lines.GetAsync(tx,
                        ControlOccurrenceWorkItemDirectorySchema.LineKey(programId, plan.ControlId),
                        ct).ConfigureAwait(false) ?? throw new InvalidOperationException(
                        "A projected control plan must have its source line state.");
                    linesByControl.Add(plan.ControlId, line);
                }
                if (line.TenantId != tenantId || line.ProgramId != programId ||
                    line.ControlId != plan.ControlId || plan.Revision > line.PlanRevision)
                    return Result<IReadOnlyList<WorkCandidate>>.Failure(InvalidScope());
            }
            cursor = page.NextCursor;
        } while (cursor is not null);

        var occurrencesByControl = new Dictionary<Uuid, Dictionary<Uuid,
            ControlOccurrenceWorkState>>();
        cursor = null;
        do
        {
            var page = await ControlOccurrenceWorkItemDirectorySchema.Occurrences.QueryAsync(tx,
                ControlOccurrenceWorkItemDirectorySchema.OccurrencesByProgram.Query()
                    .WithPrefix(programId.ToString()).Take(200).After(cursor), ct)
                .ConfigureAwait(false);
            foreach (var occurrence in page.Items)
            {
                if (occurrence.TenantId != tenantId || occurrence.ProgramId != programId ||
                    occurrence.ControlId == Uuid.Empty || occurrence.OccurrenceId == Uuid.Empty ||
                    occurrence.Revision < 1 || occurrence.ControlVersionId == Uuid.Empty ||
                    occurrence.PlanVersionId == Uuid.Empty || !IsHolder(occurrence.Assignee) ||
                    !IsOccurrenceState(occurrence.State) ||
                    !plansByControl.TryGetValue(occurrence.ControlId, out var controlPlans) ||
                    !controlPlans.TryGetValue(occurrence.PlanVersionId, out var plan) ||
                    plan.ControlVersionId != occurrence.ControlVersionId ||
                    occurrence.LatestAttestation is { } attestation &&
                    (attestation.AttestationId == Uuid.Empty || attestation.Version < 1 ||
                     attestation.RecorderMemberId == Uuid.Empty ||
                     !IsHolder(attestation.PerformedBy)) ||
                    occurrence.State is Submitted or Deferred &&
                    occurrence.LatestAttestation is null)
                    return Result<IReadOnlyList<WorkCandidate>>.Failure(InvalidScope());

                if (!occurrencesByControl.TryGetValue(occurrence.ControlId,
                        out var controlOccurrences))
                    occurrencesByControl[occurrence.ControlId] = controlOccurrences = [];
                if (!controlOccurrences.TryAdd(occurrence.OccurrenceId, occurrence))
                    return Result<IReadOnlyList<WorkCandidate>>.Failure(InvalidScope());
            }
            cursor = page.NextCursor;
        } while (cursor is not null);

        foreach (var (controlId, plans) in plansByControl)
        {
            if (!linesByControl.TryGetValue(controlId, out var line))
                return Result<IReadOnlyList<WorkCandidate>>.Failure(InvalidScope());
            if (line.CurrentPlanVersionId is null)
            {
                if (line.PendingPlanVersionId is not { } initialPendingPlanVersionId ||
                    !plans.TryGetValue(initialPendingPlanVersionId, out var initialPendingPlan) ||
                    initialPendingPlan.Status != PendingApproval ||
                    occurrencesByControl.ContainsKey(controlId))
                    return Result<IReadOnlyList<WorkCandidate>>.Failure(InvalidScope());
                continue;
            }

            if (!plans.TryGetValue(line.CurrentPlanVersionId.Value, out var currentPlan) ||
                currentPlan.Status != Approved ||
                line.PendingPlanVersionId is { } pendingPlanVersionId &&
                (!plans.TryGetValue(pendingPlanVersionId, out var pendingPlan) ||
                 pendingPlan.Status != PendingApproval))
                return Result<IReadOnlyList<WorkCandidate>>.Failure(InvalidScope());

            var control = await ControlOperationsSource.LoadControlAsync(sourceReader, tenantId,
                programId, controlId, ct).ConfigureAwait(false);
            if (control is null)
                continue;

            var identifier = control.ApprovedVersion?.Identifier ?? controlId.ToString();
            var versionUntil = OperatingAuthority.VersionWindows(control);
            var occurrences = occurrencesByControl.GetValueOrDefault(controlId) ?? [];
            foreach (var occurrence in occurrences.Values)
            {
                if (occurrence.State is Open or Returned && Wants(occurrence.OccurrenceId,
                        WorkSource.ControlOccurrence, workItemId))
                    candidates.Add(WorkSource.ControlOccurrenceCandidate(tenantId, programId,
                        controlId, identifier, occurrence.OccurrenceId, occurrence.PeriodStart,
                        occurrence.DueOn, occurrence.Assignee, currentPlan.BackupOwner));

                if (occurrence.State is Submitted or Deferred &&
                    occurrence.LatestAttestation is { } attestation &&
                    Wants(occurrence.OccurrenceId, WorkSource.OccurrenceReview, workItemId))
                    candidates.Add(WorkSource.OccurrenceReviewCandidate(tenantId, programId,
                        controlId, identifier, occurrence.OccurrenceId, occurrence.PeriodStart,
                        occurrence.DueOn, currentPlan.ReviewerMemberId, attestation.PerformedBy,
                        attestation.RecorderMemberId, attestation.RecordedAt));
            }

            var generatedOccurrenceIds = new HashSet<Uuid>();
            foreach (var plan in plans.Values.Where(static plan =>
                         plan.Status is Approved or Superseded))
            {
                var until = Earliest(plan.EffectiveUntil,
                    versionUntil.TryGetValue(plan.ControlVersionId, out var versionEnd)
                        ? versionEnd : null);
                foreach (var period in ControlCadenceSchedule.Periods(plan.Cadence,
                             plan.EffectiveFrom, until, horizon))
                {
                    var occurrenceId = ControlCadenceSchedule.OccurrenceId(controlId,
                        period.Start);
                    if (occurrences.ContainsKey(occurrenceId) ||
                        !generatedOccurrenceIds.Add(occurrenceId) ||
                        !Wants(occurrenceId, WorkSource.ControlOccurrence, workItemId))
                        continue;
                    candidates.Add(WorkSource.ControlOccurrenceCandidate(tenantId, programId,
                        controlId, identifier, occurrenceId, period.Start, period.DueOn,
                        currentPlan.Owner, currentPlan.BackupOwner));
                }
            }
        }

        return Result<IReadOnlyList<WorkCandidate>>.Success(candidates);
    }

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case ControlOperatingPlanProposed proposed:
                await ApplyPlanProposalAsync(proposed, ct).ConfigureAwait(false);
                break;
            case ControlOperatingPlanApproved approved:
                await ApplyPlanApprovalAsync(approved, ct).ConfigureAwait(false);
                break;
            case ControlOccurrenceOpened opened:
                await ApplyOccurrenceOpenedAsync(opened, ct).ConfigureAwait(false);
                break;
            case ControlOccurrenceAttested attested:
                await ApplyOccurrenceAttestedAsync(attested, ct).ConfigureAwait(false);
                break;
            case ControlOccurrenceReviewed reviewed:
                await ApplyOccurrenceReviewedAsync(reviewed, ct).ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException(
                    $"The control occurrence projector cannot apply {domainEvent.GetType().Name}.");
        }

        await IncrementRevisionAsync(ct).ConfigureAwait(false);
    }

    async ValueTask ApplyPlanProposalAsync(ControlOperatingPlanProposed proposed,
        CancellationToken ct)
    {
        var plan = proposed.Plan;
        if (proposed.TenantId == Uuid.Empty || proposed.ProgramId == Uuid.Empty ||
            proposed.ControlId == Uuid.Empty || proposed.Revision < 1 ||
            plan is null || plan.TenantId != proposed.TenantId ||
            plan.ProgramId != proposed.ProgramId || plan.ControlId != proposed.ControlId ||
            plan.Revision != proposed.Revision || plan.Status != PendingApproval ||
            plan.PlanVersionId == Uuid.Empty || plan.ControlVersionId == Uuid.Empty ||
            plan.ProposerMemberId == Uuid.Empty ||
            !HasMemberActor(plan.ProposedBy, plan.ProposerMemberId) ||
            plan.ReviewerMemberId == Uuid.Empty || !IsHolder(plan.Owner) ||
            plan.BackupOwner is not null && !IsHolder(plan.BackupOwner) ||
            ControlCadenceSchedule.Validate(plan.Cadence) is not null)
            throw new InvalidOperationException(
                "A control operating-plan proposal must preserve its scope, cadence, and proposer.");

        var key = ControlOccurrenceWorkItemDirectorySchema.LineKey(proposed.ProgramId,
            proposed.ControlId);
        var current = await ControlOccurrenceWorkItemDirectorySchema.Lines.GetAsync(Transaction,
            key, ct).ConfigureAwait(false);
        if (current is null)
        {
            if (proposed.Revision != 1)
                throw new InvalidOperationException(
                    "A control operating-plan line must begin at revision one.");
        }
        else
        {
            ValidateNextPlanRevision(current, proposed.TenantId, proposed.ProgramId,
                proposed.ControlId, proposed.Revision);
            if (current.PendingPlanVersionId is not null)
                throw new InvalidOperationException(
                    "A control operating-plan line cannot project a second pending proposal.");
        }

        var planKey = ControlOccurrenceWorkItemDirectorySchema.PlanKey(proposed.ProgramId,
            proposed.ControlId, plan.PlanVersionId);
        if (await ControlOccurrenceWorkItemDirectorySchema.Plans.GetAsync(Transaction, planKey, ct)
                .ConfigureAwait(false) is not null)
            throw new InvalidOperationException(
                "A control operating-plan version cannot be proposed more than once.");

        var workState = PlanState(proposed, plan, PendingApproval);
        await ControlOccurrenceWorkItemDirectorySchema.Plans.InsertAsync(Transaction, workState,
            ct).ConfigureAwait(false);
        var line = new ControlOccurrenceLineWorkState(proposed.TenantId, proposed.ProgramId,
            proposed.ControlId, proposed.Revision, current?.CurrentPlanVersionId, plan.PlanVersionId);
        if (current is null)
            await ControlOccurrenceWorkItemDirectorySchema.Lines.InsertAsync(Transaction, line, ct)
                .ConfigureAwait(false);
        else
            await ControlOccurrenceWorkItemDirectorySchema.Lines.ReplaceAsync(Transaction, current,
                line, ct).ConfigureAwait(false);
    }

    async ValueTask ApplyPlanApprovalAsync(ControlOperatingPlanApproved approved,
        CancellationToken ct)
    {
        if (approved.TenantId == Uuid.Empty || approved.ProgramId == Uuid.Empty ||
            approved.ControlId == Uuid.Empty || approved.PlanVersionId == Uuid.Empty ||
            approved.ApproverMemberId == Uuid.Empty ||
            !HasMemberActor(approved.ApprovedBy, approved.ApproverMemberId) ||
            approved.Reassignments is null)
            throw new InvalidOperationException(
                "A control operating-plan approval must preserve its source and approver.");

        var lineKey = ControlOccurrenceWorkItemDirectorySchema.LineKey(approved.ProgramId,
            approved.ControlId);
        var current = await ControlOccurrenceWorkItemDirectorySchema.Lines.GetAsync(Transaction,
            lineKey, ct).ConfigureAwait(false) ?? throw new InvalidOperationException(
            "A control operating plan cannot be approved before its proposal is projected.");
        ValidateNextPlanRevision(current, approved.TenantId, approved.ProgramId,
            approved.ControlId, approved.Revision);
        if (current.PendingPlanVersionId != approved.PlanVersionId)
            throw new InvalidOperationException(
                "An approval must resolve the current pending control operating plan.");

        var planKey = ControlOccurrenceWorkItemDirectorySchema.PlanKey(approved.ProgramId,
            approved.ControlId, approved.PlanVersionId);
        var pending = await ControlOccurrenceWorkItemDirectorySchema.Plans.GetAsync(Transaction,
            planKey, ct).ConfigureAwait(false) ?? throw new InvalidOperationException(
            "A pending operating plan must exist before it is approved.");
        if (pending.TenantId != approved.TenantId || pending.ProgramId != approved.ProgramId ||
            pending.ControlId != approved.ControlId || pending.Status != PendingApproval ||
            pending.Revision + 1 != approved.Revision)
            throw new InvalidOperationException(
                "An approval cannot change a plan outside its pending source scope.");

        if (current.CurrentPlanVersionId is { } previousId)
        {
            var previousKey = ControlOccurrenceWorkItemDirectorySchema.PlanKey(approved.ProgramId,
                approved.ControlId, previousId);
            var previous = await ControlOccurrenceWorkItemDirectorySchema.Plans.GetAsync(
                Transaction, previousKey, ct).ConfigureAwait(false) ?? throw new InvalidOperationException(
                "The current operating plan must exist before it is superseded.");
            if (previous.Status != Approved || pending.EffectiveFrom <= previous.EffectiveFrom)
                throw new InvalidOperationException(
                    "A replacement plan must advance the current approved plan interval.");
            await ControlOccurrenceWorkItemDirectorySchema.Plans.ReplaceAsync(Transaction, previous,
                previous with { Status = Superseded, EffectiveUntil = pending.EffectiveFrom }, ct)
                .ConfigureAwait(false);
        }

        var approvedPlan = pending with { Revision = approved.Revision, Status = Approved };
        await ControlOccurrenceWorkItemDirectorySchema.Plans.ReplaceAsync(Transaction, pending,
            approvedPlan, ct).ConfigureAwait(false);

        foreach (var reassignment in approved.Reassignments)
            await ApplyReassignmentAsync(approved, approvedPlan, reassignment, ct)
                .ConfigureAwait(false);

        var next = current with
        {
            PlanRevision = approved.Revision,
            CurrentPlanVersionId = approved.PlanVersionId,
            PendingPlanVersionId = null,
        };
        await ControlOccurrenceWorkItemDirectorySchema.Lines.ReplaceAsync(Transaction, current,
            next, ct).ConfigureAwait(false);
    }

    async ValueTask ApplyReassignmentAsync(ControlOperatingPlanApproved approved,
        ControlOccurrencePlanWorkState plan, OccurrenceReassignmentView reassignment,
        CancellationToken ct)
    {
        if (reassignment is null || reassignment.OccurrenceId == Uuid.Empty ||
            reassignment.PlanVersionId != approved.PlanVersionId ||
            reassignment.To != plan.Owner || reassignment.From is null)
            throw new InvalidOperationException(
                "A plan approval reassignment must target its approved owner and plan.");
        var key = ControlOccurrenceWorkItemDirectorySchema.OccurrenceKey(approved.ProgramId,
            reassignment.OccurrenceId);
        var occurrence = await ControlOccurrenceWorkItemDirectorySchema.Occurrences.GetAsync(
            Transaction, key, ct).ConfigureAwait(false) ?? throw new InvalidOperationException(
            "An occurrence reassignment must refer to a projected open occurrence.");
        if (occurrence.TenantId != approved.TenantId || occurrence.ProgramId != approved.ProgramId ||
            occurrence.ControlId != approved.ControlId ||
            occurrence.Assignee != reassignment.From || occurrence.State is not (Open or Returned))
            throw new InvalidOperationException(
                "An occurrence reassignment cannot change work outside its current source scope.");
        await ControlOccurrenceWorkItemDirectorySchema.Occurrences.ReplaceAsync(Transaction,
            occurrence, occurrence with { Assignee = reassignment.To }, ct).ConfigureAwait(false);
    }

    async ValueTask ApplyOccurrenceOpenedAsync(ControlOccurrenceOpened opened,
        CancellationToken ct)
    {
        if (opened.TenantId == Uuid.Empty || opened.ProgramId == Uuid.Empty ||
            opened.ControlId == Uuid.Empty || opened.OccurrenceId == Uuid.Empty ||
            opened.Revision != 1 || opened.ControlVersionId == Uuid.Empty ||
            opened.PlanVersionId == Uuid.Empty || !IsHolder(opened.Assignee))
            throw new InvalidOperationException(
                "A control occurrence must begin at revision one with complete source identity.");
        var planKey = ControlOccurrenceWorkItemDirectorySchema.PlanKey(opened.ProgramId,
            opened.ControlId, opened.PlanVersionId);
        var plan = await ControlOccurrenceWorkItemDirectorySchema.Plans.GetAsync(Transaction,
            planKey, ct).ConfigureAwait(false);
        if (plan is null || plan.TenantId != opened.TenantId ||
            plan.ControlVersionId != opened.ControlVersionId ||
            plan.Status is not (Approved or Superseded))
            throw new InvalidOperationException(
                "A control occurrence must refer to an approved plan and control version.");

        var key = ControlOccurrenceWorkItemDirectorySchema.OccurrenceKey(opened.ProgramId,
            opened.OccurrenceId);
        if (await ControlOccurrenceWorkItemDirectorySchema.Occurrences.GetAsync(Transaction, key,
                ct).ConfigureAwait(false) is not null)
            throw new InvalidOperationException(
                "A control occurrence can be opened only once in the work projection.");
        var state = new ControlOccurrenceWorkState(opened.TenantId, opened.ProgramId,
            opened.ControlId, opened.OccurrenceId, opened.Revision, opened.Kind,
            opened.ControlVersionId, opened.PlanVersionId, opened.PeriodStart, opened.PeriodEnd,
            opened.DueOn, opened.Assignee, Open, null);
        await ControlOccurrenceWorkItemDirectorySchema.Occurrences.InsertAsync(Transaction, state,
            ct).ConfigureAwait(false);
    }

    async ValueTask ApplyOccurrenceAttestedAsync(ControlOccurrenceAttested attested,
        CancellationToken ct)
    {
        var key = ControlOccurrenceWorkItemDirectorySchema.OccurrenceKey(attested.ProgramId,
            attested.OccurrenceId);
        var current = await ControlOccurrenceWorkItemDirectorySchema.Occurrences.GetAsync(
            Transaction, key, ct).ConfigureAwait(false);
        var attestation = attested.Attestation;
        if (current is null || current.TenantId != attested.TenantId ||
            current.ProgramId != attested.ProgramId || current.ControlId != attested.ControlId ||
            attested.Revision != checked(current.Revision + 1) || attestation is null ||
            attestation.AttestationId == Uuid.Empty || attestation.Version !=
                (current.LatestAttestation?.Version ?? 0) + 1 ||
            attestation.ControlVersionId != current.ControlVersionId ||
            attestation.PlanVersionId != current.PlanVersionId ||
            attestation.RecorderMemberId == Uuid.Empty ||
            !HasMemberActor(attestation.RecordedBy, attestation.RecorderMemberId) ||
            !IsHolder(attestation.PerformedBy) ||
            attestation.SupersedesAttestationId != current.LatestAttestation?.AttestationId)
            throw new InvalidOperationException(
                "An attestation must advance its occurrence and preserve its current source version.");

        var latest = new ControlOccurrenceAttestationWorkState(attestation.AttestationId,
            attestation.Version, attestation.RecorderMemberId, attestation.PerformedBy,
            attestation.RecordedAt);
        await ControlOccurrenceWorkItemDirectorySchema.Occurrences.ReplaceAsync(Transaction,
            current, current with
            {
                Revision = attested.Revision,
                State = Submitted,
                LatestAttestation = latest,
            }, ct).ConfigureAwait(false);
    }

    async ValueTask ApplyOccurrenceReviewedAsync(ControlOccurrenceReviewed reviewed,
        CancellationToken ct)
    {
        var key = ControlOccurrenceWorkItemDirectorySchema.OccurrenceKey(reviewed.ProgramId,
            reviewed.OccurrenceId);
        var current = await ControlOccurrenceWorkItemDirectorySchema.Occurrences.GetAsync(
            Transaction, key, ct).ConfigureAwait(false);
        var decision = reviewed.Review;
        if (current is null || current.TenantId != reviewed.TenantId ||
            current.ProgramId != reviewed.ProgramId || current.ControlId != reviewed.ControlId ||
            reviewed.Revision != checked(current.Revision + 1) ||
            current.State is not (Submitted or Deferred) || decision is null ||
            current.LatestAttestation is not { } attestation ||
            decision.DecisionId == Uuid.Empty || decision.AttestationId != attestation.AttestationId ||
            decision.AttestationVersion != attestation.Version ||
            decision.ReviewerMemberId == Uuid.Empty ||
            !HasMemberActor(decision.ReviewedBy, decision.ReviewerMemberId) ||
            decision.Outcome is not ("approved" or Returned or "action_requested" or Deferred))
            throw new InvalidOperationException(
                "A review must resolve the current attestation and advance its occurrence revision.");

        await ControlOccurrenceWorkItemDirectorySchema.Occurrences.ReplaceAsync(Transaction,
            current, current with { Revision = reviewed.Revision, State = decision.Outcome }, ct)
            .ConfigureAwait(false);
    }

    async ValueTask IncrementRevisionAsync(CancellationToken ct)
    {
        var current = await ControlOccurrenceWorkItemDirectorySchema.Revisions.GetAsync(
            Transaction, RevisionKey, ct).ConfigureAwait(false);
        var next = new AccountableWorkItemProjectionRevision(ProjectorName,
            checked((current?.Revision ?? 0) + 1));
        if (current is null)
            await ControlOccurrenceWorkItemDirectorySchema.Revisions.InsertAsync(Transaction,
                next, ct).ConfigureAwait(false);
        else
            await ControlOccurrenceWorkItemDirectorySchema.Revisions.ReplaceAsync(Transaction,
                current, next, ct).ConfigureAwait(false);
    }

    static ControlOccurrencePlanWorkState PlanState(ControlOperatingPlanProposed proposed,
        ControlOperatingPlanView plan, string status) => new(proposed.TenantId,
        proposed.ProgramId, proposed.ControlId, plan.PlanVersionId, plan.ControlVersionId,
        proposed.Revision, status, plan.Owner, plan.BackupOwner, plan.ReviewerMemberId,
        ControlCadenceSchedule.Clean(plan.Cadence), plan.EffectiveFrom, plan.EffectiveUntil);

    static void ValidateNextPlanRevision(ControlOccurrenceLineWorkState current, Uuid tenantId,
        Uuid programId, Uuid controlId, long revision)
    {
        if (current.TenantId != tenantId || current.ProgramId != programId ||
            current.ControlId != controlId || revision != checked(current.PlanRevision + 1))
            throw new InvalidOperationException(
                "A control operating-plan event must advance its existing line revision and scope.");
    }

    static bool Wants(Uuid occurrenceId, string kind, Uuid? workItemId) =>
        workItemId is not { } wanted || WorkCandidate.IdFor(occurrenceId, kind) == wanted;

    static bool IsOccurrenceState(string state) => state is Open or Submitted or Deferred or
        Returned or "approved" or "action_requested";

    static bool IsHolder(OperatingHolder? holder) => holder is not null &&
        holder.Id != Uuid.Empty && holder.Kind is "member" or "person" or "team";

    static bool HasMemberActor(ActorReference actor, Uuid memberId) =>
        actor.Kind == "member" && StringComparer.Ordinal.Equals(actor.Id, memberId.ToString());

    static DateOnly? Earliest(DateOnly? first, DateOnly? second) =>
        first is null ? second : second is null ? first : first < second ? first : second;

    static RequestError InvalidScope() => new(RequestErrorKind.Conflict,
        "The control occurrence work projection has an invalid tenant or program scope.");
}

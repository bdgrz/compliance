using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Work;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

sealed class FitzControlOperatingPlanWorkItemDirectory(IKvClient client,
    IAggregateReader sourceReader)
    : FitzKvProjectionStore(client,
        "kv://bdgrz/accountable-work-items/control-operating-plans-v1", ProjectorName),
      IAccountableWorkItemDirectoryReader, IControlOperatingPlanWorkItemProjection
{
    public const string ProjectorName = "AccountableWorkItemControlOperatingPlanV1";
    const string RevisionKey = ProjectorName;

    string IAccountableWorkItemDirectoryReader.ProjectorName => ProjectorName;

    public IReadOnlyCollection<string> ProjectedKinds =>
        [WorkSource.ControlOperatingPlanApproval];

    public EventStreamPattern SourcePattern(Uuid tenantId) =>
        EventStreamPattern.ForPattern(tenantId.ToString(), "control-operations");

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) => base.LoadCheckpointAsync(new CheckpointIdentity(
        ProjectorName, SourcePattern(tenantId)), ct);

    public async ValueTask<long> LoadRevisionAsync(Uuid tenantId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return (await ControlOperatingPlanWorkItemDirectorySchema.Revisions.GetAsync(tx,
            RevisionKey, ct).ConfigureAwait(false))?.Revision ?? 0;
    }

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        var scope = ScopeFor(domainEvent);
        if (scope.TenantId == Uuid.Empty || scope.ProgramId == Uuid.Empty ||
            scope.ControlId == Uuid.Empty)
            throw new InvalidOperationException(
                "A control operating-plan event must have complete tenant, program, and control scope.");

        var key = ControlOperatingPlanWorkItemDirectorySchema.LineKey(scope.ProgramId,
            scope.ControlId);
        var current = await ControlOperatingPlanWorkItemDirectorySchema.Lines.GetAsync(
            Transaction, key, ct).ConfigureAwait(false);
        ControlOperatingPlanLineWorkState next;
        switch (domainEvent)
        {
            case ControlOperatingPlanProposed proposed:
                next = await ProposeAsync(current, proposed, scope, ct).ConfigureAwait(false);
                if (current is null)
                    await ControlOperatingPlanWorkItemDirectorySchema.Lines.InsertAsync(
                        Transaction, next, ct).ConfigureAwait(false);
                else
                    await ControlOperatingPlanWorkItemDirectorySchema.Lines.ReplaceAsync(
                        Transaction, current, next, ct).ConfigureAwait(false);
                break;
            case ControlOperatingPlanApproved approved:
                next = await ApproveAsync(current, approved, scope, ct).ConfigureAwait(false);
                await ControlOperatingPlanWorkItemDirectorySchema.Lines.ReplaceAsync(
                    Transaction, current!, next, ct).ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException(
                    $"The control operating-plan projector cannot apply {domainEvent.GetType().Name}.");
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
            var page = await ControlOperatingPlanWorkItemDirectorySchema.Plans.QueryAsync(tx,
                ControlOperatingPlanWorkItemDirectorySchema.ByProgram.Query()
                    .WithPrefix(programId.ToString()).Take(200).After(cursor), ct)
                .ConfigureAwait(false);
            foreach (var plan in page.Items)
            {
                if (plan.TenantId != tenantId || plan.ProgramId != programId ||
                    plan.ControlId == Uuid.Empty || plan.PlanVersionId == Uuid.Empty ||
                    plan.ControlVersionId == Uuid.Empty || plan.ProposerMemberId == Uuid.Empty ||
                    plan.Revision < 1)
                    return Result<IReadOnlyList<WorkCandidate>>.Failure(InvalidScope());

                var control = await ControlOperationsSource.LoadControlAsync(sourceReader, tenantId,
                    programId, plan.ControlId, ct).ConfigureAwait(false);
                if (control is null || control.IsRetired ||
                    control.ApprovedVersion is not { Status: ControlOperationsLedger.Approved } current ||
                    current.VersionId != plan.ControlVersionId)
                    continue;

                candidates.Add(WorkSource.ControlOperatingPlanApprovalCandidate(tenantId,
                    programId, plan.ControlId, current.Identifier, plan.PlanVersionId,
                    plan.ProposerMemberId, plan.ProposedAt));
            }
            cursor = page.NextCursor;
        } while (cursor is not null);

        return Result<IReadOnlyList<WorkCandidate>>.Success(candidates);
    }

    async ValueTask<ControlOperatingPlanLineWorkState> ProposeAsync(
        ControlOperatingPlanLineWorkState? current, ControlOperatingPlanProposed proposed,
        (Uuid TenantId, Uuid ProgramId, Uuid ControlId, long Revision) scope,
        CancellationToken ct)
    {
        var plan = proposed.Plan;
        if (plan is null || plan.TenantId != proposed.TenantId ||
            plan.ProgramId != proposed.ProgramId || plan.ControlId != proposed.ControlId ||
            plan.Revision != proposed.Revision || plan.Status != ControlOperationsLedger.PendingApproval ||
            plan.PlanVersionId == Uuid.Empty || plan.ControlVersionId == Uuid.Empty ||
            plan.ProposerMemberId == Uuid.Empty ||
            !HasMemberActor(plan.ProposedBy, plan.ProposerMemberId))
            throw new InvalidOperationException(
                "A control operating-plan proposal must preserve its scope, revision, version, and proposer.");

        if (current is null)
        {
            if (scope.Revision != 1)
                throw new InvalidOperationException(
                    "A control operating-plan line must begin at revision one.");
        }
        else
        {
            ValidateNextRevision(current, scope);
            if (current.PendingPlanVersionId is not null)
                throw new InvalidOperationException(
                    "A control operating-plan line cannot project a second pending proposal.");
        }

        var existing = await ControlOperatingPlanWorkItemDirectorySchema.Plans.GetAsync(
            Transaction, plan.PlanVersionId, ct).ConfigureAwait(false);
        if (existing is not null)
            throw new InvalidOperationException(
                "A control operating-plan version cannot create more than one pending approval.");
        var work = new ControlOperatingPlanApprovalWorkState(proposed.TenantId,
            proposed.ProgramId, proposed.ControlId, plan.PlanVersionId, plan.ControlVersionId,
            proposed.Revision, plan.ProposerMemberId, plan.ProposedAt);
        await ControlOperatingPlanWorkItemDirectorySchema.Plans.InsertAsync(Transaction, work, ct)
            .ConfigureAwait(false);
        return new ControlOperatingPlanLineWorkState(proposed.TenantId, proposed.ProgramId,
            proposed.ControlId, proposed.Revision, plan.PlanVersionId);
    }

    async ValueTask<ControlOperatingPlanLineWorkState> ApproveAsync(
        ControlOperatingPlanLineWorkState? current, ControlOperatingPlanApproved approved,
        (Uuid TenantId, Uuid ProgramId, Uuid ControlId, long Revision) scope,
        CancellationToken ct)
    {
        if (current is null)
            throw new InvalidOperationException(
                "A control operating plan cannot be approved before its proposal is projected.");
        ValidateNextRevision(current, scope);
        if (current.PendingPlanVersionId != approved.PlanVersionId ||
            approved.PlanVersionId == Uuid.Empty || approved.ApproverMemberId == Uuid.Empty ||
            !HasMemberActor(approved.ApprovedBy, approved.ApproverMemberId))
            throw new InvalidOperationException(
                "An approval must resolve the current pending control operating plan.");

        var plan = await ControlOperatingPlanWorkItemDirectorySchema.Plans.GetAsync(Transaction,
            approved.PlanVersionId, ct).ConfigureAwait(false);
        if (plan is null || plan.TenantId != approved.TenantId ||
            plan.ProgramId != approved.ProgramId || plan.ControlId != approved.ControlId ||
            plan.Revision != current.Revision)
            throw new InvalidOperationException(
                "A control operating plan approval cannot remove work outside its pending source scope.");
        await ControlOperatingPlanWorkItemDirectorySchema.Plans.DeleteAsync(Transaction, plan, ct)
            .ConfigureAwait(false);
        return current with { Revision = approved.Revision, PendingPlanVersionId = null };
    }

    async ValueTask IncrementRevisionAsync(CancellationToken ct)
    {
        var current = await ControlOperatingPlanWorkItemDirectorySchema.Revisions.GetAsync(
            Transaction, RevisionKey, ct).ConfigureAwait(false);
        var next = new AccountableWorkItemProjectionRevision(ProjectorName,
            checked((current?.Revision ?? 0) + 1));
        if (current is null)
            await ControlOperatingPlanWorkItemDirectorySchema.Revisions.InsertAsync(Transaction,
                next, ct).ConfigureAwait(false);
        else
            await ControlOperatingPlanWorkItemDirectorySchema.Revisions.ReplaceAsync(Transaction,
                current, next, ct).ConfigureAwait(false);
    }

    static void ValidateNextRevision(ControlOperatingPlanLineWorkState current,
        (Uuid TenantId, Uuid ProgramId, Uuid ControlId, long Revision) scope)
    {
        if (current.TenantId != scope.TenantId || current.ProgramId != scope.ProgramId ||
            current.ControlId != scope.ControlId ||
            scope.Revision != checked(current.Revision + 1))
            throw new InvalidOperationException(
                "A control operating-plan event must advance its existing source revision and scope.");
    }

    static bool HasMemberActor(ActorReference actor, Uuid memberId) =>
        actor.Kind == "member" && StringComparer.Ordinal.Equals(actor.Id, memberId.ToString());

    static (Uuid TenantId, Uuid ProgramId, Uuid ControlId, long Revision) ScopeFor(
        DomainEvent domainEvent) => domainEvent switch
        {
            ControlOperatingPlanProposed ev =>
                (ev.TenantId, ev.ProgramId, ev.ControlId, ev.Revision),
            ControlOperatingPlanApproved ev =>
                (ev.TenantId, ev.ProgramId, ev.ControlId, ev.Revision),
            _ => throw new InvalidOperationException(
                $"The control operating-plan projector cannot apply {domainEvent.GetType().Name}.")
        };

    static RequestError InvalidScope() => new(RequestErrorKind.Conflict,
        "The control operating-plan work projection has an invalid tenant or program scope.");
}

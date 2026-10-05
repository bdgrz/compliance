using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.ControlMappings;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

sealed class FitzControlMappingWorkItemDirectory(IKvClient client)
    : FitzKvProjectionStore(client,
        "kv://bdgrz/accountable-work-items/control-criterion-mappings-v1", ProjectorName),
      IAccountableWorkItemDirectoryReader, IControlCriterionMappingWorkItemProjection
{
    public const string ProjectorName = "AccountableWorkItemControlCriterionMappingV1";
    const string RevisionKey = ProjectorName;
    const string Pending = "pending";
    const string Active = "active";
    const string Rejected = "rejected";
    const string Retired = "retired";

    string IAccountableWorkItemDirectoryReader.ProjectorName => ProjectorName;

    public IReadOnlyCollection<string> ProjectedKinds =>
        [WorkSource.ControlCriterionMappingReview];

    public EventStreamPattern SourcePattern(Uuid tenantId) =>
        EventStreamPattern.ForPattern(tenantId.ToString(), "control-criterion-mappings");

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) => base.LoadCheckpointAsync(new CheckpointIdentity(
        ProjectorName, SourcePattern(tenantId)), ct);

    public async ValueTask<long> LoadRevisionAsync(Uuid tenantId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return (await ControlCriterionMappingWorkItemDirectorySchema.Revisions.GetAsync(tx,
            RevisionKey, ct).ConfigureAwait(false))?.Revision ?? 0;
    }

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        var scope = ScopeFor(domainEvent);
        if (scope.TenantId == Uuid.Empty || scope.ProgramId == Uuid.Empty ||
            scope.MappingId == Uuid.Empty)
            throw new InvalidOperationException(
                "A control mapping event must have a complete tenant and mapping scope.");

        var key = ControlCriterionMappingWorkItemDirectorySchema.MappingKey(scope.ProgramId,
            scope.MappingId);
        var current = await ControlCriterionMappingWorkItemDirectorySchema.Mappings.GetAsync(
            Transaction, key, ct).ConfigureAwait(false);
        ControlCriterionMappingWorkState next;
        switch (domainEvent)
        {
            case ControlCriterionMappingProposed proposed:
                next = await ProposeAsync(current, proposed, scope, ct).ConfigureAwait(false);
                if (current is null)
                    await ControlCriterionMappingWorkItemDirectorySchema.Mappings.InsertAsync(
                        Transaction, next, ct).ConfigureAwait(false);
                else
                    await ControlCriterionMappingWorkItemDirectorySchema.Mappings.ReplaceAsync(
                        Transaction, current, next, ct).ConfigureAwait(false);
                break;
            case ControlCriterionMappingReviewed reviewed:
                next = await ReviewAsync(current, reviewed, scope, ct).ConfigureAwait(false);
                await ControlCriterionMappingWorkItemDirectorySchema.Mappings.ReplaceAsync(
                    Transaction, current!, next, ct).ConfigureAwait(false);
                break;
            case ControlCriterionMappingRetired retired:
                next = RetireAsync(current, retired, scope);
                await ControlCriterionMappingWorkItemDirectorySchema.Mappings.ReplaceAsync(
                    Transaction, current!, next, ct).ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException(
                    $"The control mapping projector cannot apply {domainEvent.GetType().Name}.");
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
            var page = await ControlCriterionMappingWorkItemDirectorySchema.WorkItems.QueryAsync(tx,
                ControlCriterionMappingWorkItemDirectorySchema.ByProgram.Query()
                    .WithPrefix(programId.ToString()).Take(200).After(cursor), ct)
                .ConfigureAwait(false);
            foreach (var item in page.Items)
            {
                var state = await ControlCriterionMappingWorkItemDirectorySchema.Mappings.GetAsync(tx,
                    ControlCriterionMappingWorkItemDirectorySchema.MappingKey(programId,
                        item.SourceId), ct).ConfigureAwait(false);
                if (item.TenantId != tenantId || item.ProgramId != programId ||
                    item.Kind != WorkSource.ControlCriterionMappingReview ||
                    item.ControlId != state?.ControlId || item.FindingId is not null ||
                    item.RiskId is not null || state is null || state.TenantId != tenantId ||
                    state.ProgramId != programId || state.MappingId != item.SourceId ||
                    state.State != Pending || state.PendingVersionNumber is not { } pending ||
                    state.ProposerMemberId is not { } proposer || !item.Excluded.Contains(proposer) ||
                    state.ProposedAt != item.CreatedAt ||
                    item.WorkItemId != WorkItemIdFor(state.MappingId, pending))
                    return Result<IReadOnlyList<WorkCandidate>>.Failure(InvalidScope());
                candidates.Add(item.ToCandidate());
            }
            cursor = page.NextCursor;
        } while (cursor is not null);

        return Result<IReadOnlyList<WorkCandidate>>.Success(candidates);
    }

    async ValueTask<ControlCriterionMappingWorkState> ProposeAsync(
        ControlCriterionMappingWorkState? current, ControlCriterionMappingProposed proposed,
        (Uuid TenantId, Uuid ProgramId, Uuid MappingId, long Revision) scope,
        CancellationToken ct)
    {
        if (proposed.ControlId == Uuid.Empty || proposed.ControlVersionId == Uuid.Empty ||
            proposed.EditionId == Uuid.Empty || proposed.ActorMemberId == Uuid.Empty ||
            !HasMemberActor(proposed.Actor, proposed.ActorMemberId) ||
            string.IsNullOrWhiteSpace(proposed.CriterionIdentifier) ||
            string.IsNullOrWhiteSpace(proposed.CriterionKind) ||
            proposed.MappingId != ControlCriterionMappingLedger.MappingIdFor(proposed.ProgramId,
                proposed.ControlId, proposed.EditionId, proposed.CriterionIdentifier))
            throw new InvalidOperationException(
                "A control mapping proposal must preserve its canonical program, control, edition, and criterion identity.");

        if (current is null)
        {
            if (scope.Revision != 1 || proposed.VersionNumber != 1)
                throw new InvalidOperationException(
                    "A control mapping must begin at revision and version one.");
        }
        else
        {
            ValidateNextRevision(current, scope);
            if (current.State == Pending || current.PendingVersionNumber is not null ||
                current.State is not (Active or Rejected or Retired) ||
                current.ControlId != proposed.ControlId ||
                current.EditionId != proposed.EditionId ||
                !StringComparer.Ordinal.Equals(current.CriterionIdentifier,
                    proposed.CriterionIdentifier) ||
                !StringComparer.Ordinal.Equals(current.CriterionKind, proposed.CriterionKind) ||
                proposed.VersionNumber != checked(current.LastVersionNumber + 1))
                throw new InvalidOperationException(
                    "A new proposal must advance the resolved control mapping without changing its identity.");
        }

        var candidate = WorkSource.ControlCriterionMappingReviewCandidate(proposed.TenantId,
            proposed.ProgramId, proposed.ControlId, proposed.MappingId,
            proposed.CriterionIdentifier, proposed.ActorMemberId, proposed.VersionNumber,
            proposed.ProposedAt);
        await AddWorkItemAsync(proposed.TenantId, proposed.ProgramId, candidate, ct)
            .ConfigureAwait(false);
        return new ControlCriterionMappingWorkState(proposed.TenantId, proposed.ProgramId,
            proposed.MappingId, proposed.ControlId, proposed.ControlVersionId, proposed.EditionId,
            proposed.CriterionIdentifier, proposed.CriterionKind, proposed.Revision,
            proposed.VersionNumber, current?.ActiveVersionNumber, proposed.VersionNumber,
            proposed.ActorMemberId, proposed.ProposedAt, Pending);
    }

    async ValueTask<ControlCriterionMappingWorkState> ReviewAsync(
        ControlCriterionMappingWorkState? current, ControlCriterionMappingReviewed reviewed,
        (Uuid TenantId, Uuid ProgramId, Uuid MappingId, long Revision) scope,
        CancellationToken ct)
    {
        if (current is null)
            throw new InvalidOperationException(
                "A control mapping cannot be reviewed before its proposal is projected.");
        ValidateNextRevision(current, scope);
        if (current.State != Pending || current.PendingVersionNumber != reviewed.VersionNumber ||
            reviewed.DecisionId == Uuid.Empty || reviewed.ActorMemberId == Uuid.Empty ||
            !HasMemberActor(reviewed.Actor, reviewed.ActorMemberId) ||
            reviewed.Outcome is not ("accept" or "reject"))
            throw new InvalidOperationException(
                "A control mapping review must resolve its current proposal with a valid outcome.");
        await RemoveWorkItemAsync(reviewed.TenantId, reviewed.ProgramId, reviewed.MappingId,
            reviewed.VersionNumber, required: true, ct).ConfigureAwait(false);
        return current with
        {
            Revision = reviewed.Revision,
            ActiveVersionNumber = reviewed.Outcome == "accept"
                ? reviewed.VersionNumber
                : current.ActiveVersionNumber,
            PendingVersionNumber = null,
            State = reviewed.Outcome == "accept" || current.ActiveVersionNumber is not null
                ? Active
                : Rejected,
        };
    }

    static ControlCriterionMappingWorkState RetireAsync(ControlCriterionMappingWorkState? current,
        ControlCriterionMappingRetired retired,
        (Uuid TenantId, Uuid ProgramId, Uuid MappingId, long Revision) scope)
    {
        if (current is null)
            throw new InvalidOperationException(
                "A control mapping cannot be retired before its active version is projected.");
        ValidateNextRevision(current, scope);
        if (current.State != Active || current.PendingVersionNumber is not null ||
            current.ActiveVersionNumber != retired.VersionNumber ||
            retired.ActorMemberId == Uuid.Empty ||
            !HasMemberActor(retired.Actor, retired.ActorMemberId))
            throw new InvalidOperationException(
                "Only the active control mapping version can be retired.");
        return current with { Revision = retired.Revision, ActiveVersionNumber = null, State = Retired };
    }

    async ValueTask AddWorkItemAsync(Uuid tenantId, Uuid programId, WorkCandidate candidate,
        CancellationToken ct)
    {
        var workItem = AccountableWorkItemView.FromCandidate(tenantId, programId, candidate);
        var existing = await ControlCriterionMappingWorkItemDirectorySchema.WorkItems.GetAsync(
            Transaction, candidate.WorkItemId, ct).ConfigureAwait(false);
        if (existing is null)
            await ControlCriterionMappingWorkItemDirectorySchema.WorkItems.InsertAsync(Transaction,
                workItem, ct).ConfigureAwait(false);
        else if (existing.TenantId != tenantId || existing.ProgramId != programId ||
                 existing.SourceId != candidate.SourceId || existing.Kind != candidate.Kind)
            throw new InvalidOperationException(
                "A control mapping work item cannot change its source scope.");
        else
            await ControlCriterionMappingWorkItemDirectorySchema.WorkItems.ReplaceAsync(Transaction,
                existing, workItem, ct).ConfigureAwait(false);
    }

    async ValueTask RemoveWorkItemAsync(Uuid tenantId, Uuid programId, Uuid mappingId,
        int versionNumber, bool required, CancellationToken ct)
    {
        var workItemId = WorkItemIdFor(mappingId, versionNumber);
        var existing = await ControlCriterionMappingWorkItemDirectorySchema.WorkItems.GetAsync(
            Transaction, workItemId, ct).ConfigureAwait(false);
        if (existing is null)
        {
            if (required)
                throw new InvalidOperationException(
                    "A control mapping review cannot complete before its proposal is projected.");
            return;
        }
        if (existing.TenantId != tenantId || existing.ProgramId != programId ||
            existing.SourceId != mappingId ||
            existing.Kind != WorkSource.ControlCriterionMappingReview ||
            existing.WorkItemId != workItemId)
            throw new InvalidOperationException(
                "A control mapping work item cannot be removed outside its source scope.");
        await ControlCriterionMappingWorkItemDirectorySchema.WorkItems.DeleteAsync(Transaction,
            existing, ct).ConfigureAwait(false);
    }

    async ValueTask IncrementRevisionAsync(CancellationToken ct)
    {
        var current = await ControlCriterionMappingWorkItemDirectorySchema.Revisions.GetAsync(
            Transaction, RevisionKey, ct).ConfigureAwait(false);
        var next = new AccountableWorkItemProjectionRevision(ProjectorName,
            checked((current?.Revision ?? 0) + 1));
        if (current is null)
            await ControlCriterionMappingWorkItemDirectorySchema.Revisions.InsertAsync(Transaction,
                next, ct).ConfigureAwait(false);
        else
            await ControlCriterionMappingWorkItemDirectorySchema.Revisions.ReplaceAsync(Transaction,
                current, next, ct).ConfigureAwait(false);
    }

    static void ValidateNextRevision(ControlCriterionMappingWorkState current,
        (Uuid TenantId, Uuid ProgramId, Uuid MappingId, long Revision) scope)
    {
        if (current.TenantId != scope.TenantId || current.ProgramId != scope.ProgramId ||
            current.MappingId != scope.MappingId ||
            scope.Revision != checked(current.Revision + 1))
            throw new InvalidOperationException(
                "A control mapping event must advance its existing source revision and scope.");
    }

    static bool HasMemberActor(ActorReference actor, Uuid memberId) =>
        actor.Kind == "member" &&
        StringComparer.Ordinal.Equals(actor.Id, memberId.ToString());

    static Uuid WorkItemIdFor(Uuid mappingId, int versionNumber) =>
        WorkSource.ControlCriterionMappingReviewWorkItemId(mappingId, versionNumber);

    static (Uuid TenantId, Uuid ProgramId, Uuid MappingId, long Revision) ScopeFor(
        DomainEvent domainEvent) => domainEvent switch
        {
            ControlCriterionMappingProposed ev =>
                (ev.TenantId, ev.ProgramId, ev.MappingId, ev.Revision),
            ControlCriterionMappingReviewed ev =>
                (ev.TenantId, ev.ProgramId, ev.MappingId, ev.Revision),
            ControlCriterionMappingRetired ev =>
                (ev.TenantId, ev.ProgramId, ev.MappingId, ev.Revision),
            _ => throw new InvalidOperationException(
                $"The control mapping projector cannot apply {domainEvent.GetType().Name}.")
        };

    static RequestError InvalidScope() => new(RequestErrorKind.Conflict,
        "The control mapping work projection has an invalid tenant or program scope.");
}

using Bdgrz.Compliance.Features.ControlMappings;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

sealed class FitzCriterionApplicabilityWorkItemDirectory(IKvClient client)
    : FitzKvProjectionStore(client,
        "kv://bdgrz/accountable-work-items/criterion-applicability-v1", ProjectorName),
      IAccountableWorkItemDirectoryReader, ICriterionApplicabilityWorkItemProjection
{
    public const string ProjectorName = "AccountableWorkItemCriterionApplicabilityV1";
    const string RevisionKey = ProjectorName;
    const string Pending = "pending";
    const string NotApplicable = "not_applicable";
    const string Rejected = "rejected";
    const string Withdrawn = "withdrawn";

    string IAccountableWorkItemDirectoryReader.ProjectorName => ProjectorName;

    public IReadOnlyCollection<string> ProjectedKinds =>
        [WorkSource.CriterionApplicabilityReview];

    public EventStreamPattern SourcePattern(Uuid tenantId) =>
        EventStreamPattern.ForPattern(tenantId.ToString(), "criterion-applicability");

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) => base.LoadCheckpointAsync(new CheckpointIdentity(
        ProjectorName, SourcePattern(tenantId)), ct);

    public async ValueTask<long> LoadRevisionAsync(Uuid tenantId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return (await CriterionApplicabilityWorkItemDirectorySchema.Revisions.GetAsync(tx,
            RevisionKey, ct).ConfigureAwait(false))?.Revision ?? 0;
    }

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        var scope = ScopeFor(domainEvent);
        if (scope.TenantId == Uuid.Empty || scope.ProgramId == Uuid.Empty ||
            scope.DecisionId == Uuid.Empty)
            throw new InvalidOperationException(
                "A criterion applicability event must have a complete tenant and decision scope.");

        var key = CriterionApplicabilityWorkItemDirectorySchema.DecisionKey(scope.ProgramId,
            scope.DecisionId);
        var current = await CriterionApplicabilityWorkItemDirectorySchema.Decisions.GetAsync(
            Transaction, key, ct).ConfigureAwait(false);
        CriterionApplicabilityWorkState next;
        switch (domainEvent)
        {
            case CriterionNotApplicableProposed proposed:
                next = await ProposeAsync(current, proposed, scope, ct).ConfigureAwait(false);
                if (current is null)
                    await CriterionApplicabilityWorkItemDirectorySchema.Decisions.InsertAsync(
                        Transaction, next, ct).ConfigureAwait(false);
                else
                    await CriterionApplicabilityWorkItemDirectorySchema.Decisions.ReplaceAsync(
                        Transaction, current, next, ct).ConfigureAwait(false);
                break;
            case CriterionApplicabilityReviewed reviewed:
                next = await ReviewAsync(current, reviewed, scope, ct).ConfigureAwait(false);
                await CriterionApplicabilityWorkItemDirectorySchema.Decisions.ReplaceAsync(
                    Transaction, current!, next, ct).ConfigureAwait(false);
                break;
            case CriterionNotApplicableWithdrawn withdrawn:
                next = await WithdrawAsync(current, withdrawn, scope, ct).ConfigureAwait(false);
                await CriterionApplicabilityWorkItemDirectorySchema.Decisions.ReplaceAsync(
                    Transaction, current!, next, ct).ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException(
                    $"The criterion applicability projector cannot apply {domainEvent.GetType().Name}.");
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
            var page = await CriterionApplicabilityWorkItemDirectorySchema.WorkItems.QueryAsync(tx,
                CriterionApplicabilityWorkItemDirectorySchema.ByProgram.Query()
                    .WithPrefix(programId.ToString()).Take(200).After(cursor), ct)
                .ConfigureAwait(false);
            foreach (var item in page.Items)
            {
                var state = await CriterionApplicabilityWorkItemDirectorySchema.Decisions.GetAsync(tx,
                    CriterionApplicabilityWorkItemDirectorySchema.DecisionKey(programId,
                        item.SourceId), ct).ConfigureAwait(false);
                if (item.TenantId != tenantId || item.ProgramId != programId ||
                    item.Kind != WorkSource.CriterionApplicabilityReview ||
                    item.ControlId is not null || item.FindingId is not null || item.RiskId is not null ||
                    state is null || state.TenantId != tenantId || state.ProgramId != programId ||
                    state.DecisionId != item.SourceId || state.State != Pending ||
                    !item.Excluded.Contains(state.ProposerMemberId) ||
                    state.ProposedAt != item.CreatedAt ||
                    item.WorkItemId != WorkItemIdFor(state.DecisionId, state.VersionNumber))
                    return Result<IReadOnlyList<WorkCandidate>>.Failure(InvalidScope());
                candidates.Add(item.ToCandidate());
            }
            cursor = page.NextCursor;
        } while (cursor is not null);

        return Result<IReadOnlyList<WorkCandidate>>.Success(candidates);
    }

    async ValueTask<CriterionApplicabilityWorkState> ProposeAsync(
        CriterionApplicabilityWorkState? current, CriterionNotApplicableProposed proposed,
        (Uuid TenantId, Uuid ProgramId, Uuid DecisionId, long Revision) scope,
        CancellationToken ct)
    {
        if (proposed.EditionId == Uuid.Empty || proposed.ProposerMemberId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(proposed.CriterionIdentifier))
            throw new InvalidOperationException(
                "A not-applicable proposal must identify its edition, criterion, and proposer.");
        if (current is null)
        {
            if (scope.Revision != 1 || proposed.VersionNumber != 1)
                throw new InvalidOperationException(
                    "A criterion applicability decision must begin at revision and version one.");
        }
        else
        {
            ValidateNextRevision(current, scope);
            if (current.EditionId != proposed.EditionId ||
                !StringComparer.Ordinal.Equals(current.CriterionIdentifier,
                    proposed.CriterionIdentifier) ||
                current.State is not (Rejected or Withdrawn) ||
                proposed.VersionNumber != checked(current.VersionNumber + 1))
                throw new InvalidOperationException(
                    "A new proposal must advance the same resolved criterion applicability decision.");
        }

        var candidate = WorkSource.CriterionApplicabilityReviewCandidate(proposed.TenantId,
            proposed.ProgramId, proposed.DecisionId, proposed.CriterionIdentifier,
            proposed.ProposerMemberId, proposed.VersionNumber, proposed.ProposedAt);
        await AddWorkItemAsync(proposed.TenantId, proposed.ProgramId, candidate, ct)
            .ConfigureAwait(false);
        return new CriterionApplicabilityWorkState(proposed.TenantId, proposed.ProgramId,
            proposed.DecisionId, proposed.EditionId, proposed.CriterionIdentifier,
            proposed.Revision, proposed.VersionNumber, proposed.ProposerMemberId,
            proposed.ProposedAt, Pending);
    }

    async ValueTask<CriterionApplicabilityWorkState> ReviewAsync(
        CriterionApplicabilityWorkState? current, CriterionApplicabilityReviewed reviewed,
        (Uuid TenantId, Uuid ProgramId, Uuid DecisionId, long Revision) scope,
        CancellationToken ct)
    {
        if (current is null)
            throw new InvalidOperationException(
                "A criterion applicability proposal cannot be reviewed before it is projected.");
        ValidateNextRevision(current, scope);
        if (current.State != Pending || current.VersionNumber != reviewed.VersionNumber ||
            reviewed.Outcome is not ("accept" or "reject"))
            throw new InvalidOperationException(
                "A criterion applicability review must resolve its current proposed version.");
        await RemoveWorkItemAsync(reviewed.TenantId, reviewed.ProgramId,
            reviewed.DecisionId, reviewed.VersionNumber, required: true, ct).ConfigureAwait(false);
        return current with
        {
            Revision = reviewed.Revision,
            State = reviewed.Outcome == "accept" ? NotApplicable : Rejected,
        };
    }

    async ValueTask<CriterionApplicabilityWorkState> WithdrawAsync(
        CriterionApplicabilityWorkState? current, CriterionNotApplicableWithdrawn withdrawn,
        (Uuid TenantId, Uuid ProgramId, Uuid DecisionId, long Revision) scope,
        CancellationToken ct)
    {
        if (current is null)
            throw new InvalidOperationException(
                "A criterion applicability decision cannot be withdrawn before it is projected.");
        ValidateNextRevision(current, scope);
        if (current.State != NotApplicable || current.VersionNumber != withdrawn.VersionNumber)
            throw new InvalidOperationException(
                "Only the currently accepted criterion applicability decision can be withdrawn.");
        await RemoveWorkItemAsync(withdrawn.TenantId, withdrawn.ProgramId,
            withdrawn.DecisionId, withdrawn.VersionNumber, required: false, ct).ConfigureAwait(false);
        return current with { Revision = withdrawn.Revision, State = Withdrawn };
    }

    async ValueTask AddWorkItemAsync(Uuid tenantId, Uuid programId, WorkCandidate candidate,
        CancellationToken ct)
    {
        var workItem = AccountableWorkItemView.FromCandidate(tenantId, programId, candidate);
        var existing = await CriterionApplicabilityWorkItemDirectorySchema.WorkItems.GetAsync(
            Transaction, candidate.WorkItemId, ct).ConfigureAwait(false);
        if (existing is null)
            await CriterionApplicabilityWorkItemDirectorySchema.WorkItems.InsertAsync(Transaction,
                workItem, ct).ConfigureAwait(false);
        else if (existing.TenantId != tenantId || existing.ProgramId != programId ||
                 existing.SourceId != candidate.SourceId || existing.Kind != candidate.Kind)
            throw new InvalidOperationException(
                "A criterion applicability work item cannot change its source scope.");
        else
            await CriterionApplicabilityWorkItemDirectorySchema.WorkItems.ReplaceAsync(Transaction,
                existing, workItem, ct).ConfigureAwait(false);
    }

    async ValueTask RemoveWorkItemAsync(Uuid tenantId, Uuid programId, Uuid decisionId,
        int versionNumber, bool required, CancellationToken ct)
    {
        var workItemId = WorkItemIdFor(decisionId, versionNumber);
        var existing = await CriterionApplicabilityWorkItemDirectorySchema.WorkItems.GetAsync(
            Transaction, workItemId, ct).ConfigureAwait(false);
        if (existing is null)
        {
            if (required)
                throw new InvalidOperationException(
                    "A criterion applicability review cannot complete before its proposal is projected.");
            return;
        }
        if (existing.TenantId != tenantId || existing.ProgramId != programId ||
            existing.SourceId != decisionId ||
            existing.Kind != WorkSource.CriterionApplicabilityReview ||
            existing.WorkItemId != workItemId)
            throw new InvalidOperationException(
                "A criterion applicability work item cannot be removed outside its source scope.");
        await CriterionApplicabilityWorkItemDirectorySchema.WorkItems.DeleteAsync(Transaction,
            existing, ct).ConfigureAwait(false);
    }

    async ValueTask IncrementRevisionAsync(CancellationToken ct)
    {
        var current = await CriterionApplicabilityWorkItemDirectorySchema.Revisions.GetAsync(
            Transaction, RevisionKey, ct).ConfigureAwait(false);
        var next = new AccountableWorkItemProjectionRevision(ProjectorName,
            checked((current?.Revision ?? 0) + 1));
        if (current is null)
            await CriterionApplicabilityWorkItemDirectorySchema.Revisions.InsertAsync(Transaction,
                next, ct).ConfigureAwait(false);
        else
            await CriterionApplicabilityWorkItemDirectorySchema.Revisions.ReplaceAsync(Transaction,
                current, next, ct).ConfigureAwait(false);
    }

    static void ValidateNextRevision(CriterionApplicabilityWorkState current,
        (Uuid TenantId, Uuid ProgramId, Uuid DecisionId, long Revision) scope)
    {
        if (current.TenantId != scope.TenantId || current.ProgramId != scope.ProgramId ||
            current.DecisionId != scope.DecisionId ||
            scope.Revision != checked(current.Revision + 1))
            throw new InvalidOperationException(
                "A criterion applicability event must advance its existing source revision and scope.");
    }

    static Uuid WorkItemIdFor(Uuid decisionId, int versionNumber) =>
        WorkSource.CriterionApplicabilityReviewWorkItemId(decisionId, versionNumber);

    static (Uuid TenantId, Uuid ProgramId, Uuid DecisionId, long Revision) ScopeFor(
        DomainEvent domainEvent) => domainEvent switch
        {
            CriterionNotApplicableProposed ev =>
                (ev.TenantId, ev.ProgramId, ev.DecisionId, ev.Revision),
            CriterionApplicabilityReviewed ev =>
                (ev.TenantId, ev.ProgramId, ev.DecisionId, ev.Revision),
            CriterionNotApplicableWithdrawn ev =>
                (ev.TenantId, ev.ProgramId, ev.DecisionId, ev.Revision),
            _ => throw new InvalidOperationException(
                $"The criterion applicability projector cannot apply {domainEvent.GetType().Name}.")
        };

    static RequestError InvalidScope() => new(RequestErrorKind.Conflict,
        "The criterion applicability work projection has an invalid tenant or program scope.");
}

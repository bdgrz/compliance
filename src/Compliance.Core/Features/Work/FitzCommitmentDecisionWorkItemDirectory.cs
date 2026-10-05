using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Features.Responsibilities;
using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

sealed class FitzCommitmentDecisionWorkItemDirectory(IKvClient client,
    IAggregateReader sourceReader)
    : FitzKvProjectionStore(client,
        "kv://bdgrz/accountable-work-items/commitment-decisions-v1", ProjectorName),
      IAccountableWorkItemDirectoryReader, ICommitmentDecisionWorkItemProjection
{
    public const string ProjectorName = "AccountableWorkItemCommitmentDecisionV1";
    const string RevisionKey = ProjectorName;

    string IAccountableWorkItemDirectoryReader.ProjectorName => ProjectorName;

    public IReadOnlyCollection<string> ProjectedKinds => CommitmentDecisionWork.ProjectedKinds;

    public EventStreamPattern SourcePattern(Uuid tenantId) =>
        EventStreamPattern.ForPattern(tenantId.ToString(), "commitment-drafts");

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) => base.LoadCheckpointAsync(new CheckpointIdentity(
        ProjectorName, SourcePattern(tenantId)), ct);

    public async ValueTask<long> LoadRevisionAsync(Uuid tenantId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return (await CommitmentDecisionWorkItemDirectorySchema.Revisions.GetAsync(tx,
            RevisionKey, ct).ConfigureAwait(false))?.Revision ?? 0;
    }

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        var (tenantId, draftId) = ScopeFor(domainEvent);
        if (tenantId == Uuid.Empty || draftId == Uuid.Empty)
            throw new InvalidOperationException(
                "A commitment work event must have complete tenant and draft scope.");

        var current = await CommitmentDecisionWorkItemDirectorySchema.Commitments.GetAsync(
            Transaction, draftId, ct).ConfigureAwait(false);
        if (current is not null && (current.TenantId != tenantId || current.DraftId != draftId))
            throw new InvalidOperationException(
                "A commitment work event cannot replace state outside its recorded scope.");

        var draft = await sourceReader.HydrateAsync(new CommitmentDraft(tenantId, draftId), ct)
            .ConfigureAwait(false);
        if (!draft.IsCreated || draft.ProgramId == Uuid.Empty || draft.Revision < 1 ||
            string.IsNullOrWhiteSpace(draft.Identifier))
            throw new InvalidOperationException(
                "A commitment work event must match its authoritative source aggregate.");

        var changedAt = domainEvent switch
        {
            CommitmentDraftCreated created => created.ChangedAt,
            CommitmentDraftRevised revised => revised.ChangedAt,
            CommitmentReviewed or CommitmentApproved or ResponsibilityAssigned or
                ResponsibilityRevoked => current?.DraftChangedAt ?? throw new InvalidOperationException(
                    "A commitment decision event must follow draft creation."),
            _ => throw new InvalidOperationException(
                "A commitment work projection received an unsupported event."),
        };
        var next = CommitmentDecisionWork.FromSource(tenantId, draft, changedAt);
        var validation = CommitmentDecisionWork.Candidates(next, DateTimeOffset.MinValue, null);
        if (!validation.IsSuccess)
            throw new InvalidOperationException(validation.Error.Message);

        if (current is null)
            await CommitmentDecisionWorkItemDirectorySchema.Commitments.InsertAsync(Transaction,
                next, ct).ConfigureAwait(false);
        else
            await CommitmentDecisionWorkItemDirectorySchema.Commitments.ReplaceAsync(Transaction,
                current, next, ct).ConfigureAwait(false);

        await IncrementRevisionAsync(ct).ConfigureAwait(false);
    }

    public ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId,
        Uuid programId, CancellationToken ct = default) =>
        LoadProgramAsync(tenantId, programId, DateTimeOffset.UtcNow, DateOnly.MaxValue, null, ct);

    public ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId,
        Uuid programId, DateOnly today, DateOnly horizon, Uuid? workItemId,
        CancellationToken ct = default) => LoadProgramAsync(tenantId, programId,
        new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), horizon,
        workItemId, ct);

    public async ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId,
        Uuid programId, DateTimeOffset now, DateOnly horizon, Uuid? workItemId,
        CancellationToken ct = default)
    {
        _ = horizon;
        var candidates = new List<WorkCandidate>();
        string? cursor = null;
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        do
        {
            var page = await CommitmentDecisionWorkItemDirectorySchema.Commitments.QueryAsync(tx,
                CommitmentDecisionWorkItemDirectorySchema.ByProgram.Query()
                    .WithPrefix(programId.ToString()).Take(200).After(cursor), ct)
                .ConfigureAwait(false);
            foreach (var state in page.Items)
            {
                if (state.TenantId != tenantId || state.ProgramId != programId ||
                    state.DraftId == Uuid.Empty || state.SourceRevision < 1)
                    return Result<IReadOnlyList<WorkCandidate>>.Failure(InvalidScope());

                var work = CommitmentDecisionWork.Candidates(state, now, workItemId);
                if (!work.IsSuccess)
                    return Result<IReadOnlyList<WorkCandidate>>.Failure(work.Error);
                candidates.AddRange(work.Value);
            }
            cursor = page.NextCursor;
        } while (cursor is not null);

        return Result<IReadOnlyList<WorkCandidate>>.Success(candidates);
    }

    async ValueTask IncrementRevisionAsync(CancellationToken ct)
    {
        var current = await CommitmentDecisionWorkItemDirectorySchema.Revisions.GetAsync(
            Transaction, RevisionKey, ct).ConfigureAwait(false);
        var next = new AccountableWorkItemProjectionRevision(ProjectorName,
            checked((current?.Revision ?? 0) + 1));
        if (current is null)
            await CommitmentDecisionWorkItemDirectorySchema.Revisions.InsertAsync(Transaction,
                next, ct).ConfigureAwait(false);
        else
            await CommitmentDecisionWorkItemDirectorySchema.Revisions.ReplaceAsync(Transaction,
                current, next, ct).ConfigureAwait(false);
    }

    static (Uuid TenantId, Uuid DraftId) ScopeFor(DomainEvent domainEvent) => domainEvent switch
    {
        CommitmentDraftCreated ev => (ev.TenantId, ev.DraftId),
        CommitmentDraftRevised ev => (ev.TenantId, ev.DraftId),
        CommitmentReviewed ev => (ev.TenantId, ev.DraftId),
        CommitmentApproved ev => (ev.TenantId, ev.DraftId),
        ResponsibilityAssigned ev when ev.Scope.RecordType == SeparationOfDutiesRecordTypes.Commitment =>
            (ev.TenantId, ev.Scope.RecordId),
        ResponsibilityRevoked ev when ev.Scope.RecordType == SeparationOfDutiesRecordTypes.Commitment =>
            (ev.TenantId, ev.Scope.RecordId),
        _ => (Uuid.Empty, Uuid.Empty),
    };

    static RequestError InvalidScope() => new(RequestErrorKind.Conflict,
        "The commitment work projection has an invalid program scope.");
}

using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Responsibilities;
using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

sealed class FitzBoundaryDecisionWorkItemDirectory(IKvClient client, IAggregateReader sourceReader)
    : FitzKvProjectionStore(client,
        "kv://bdgrz/accountable-work-items/boundary-decisions-v1", ProjectorName),
      IAccountableWorkItemDirectoryReader, IBoundaryDecisionWorkItemProjection
{
    public const string ProjectorName = "AccountableWorkItemBoundaryDecisionV1";
    const string RevisionKey = ProjectorName;

    string IAccountableWorkItemDirectoryReader.ProjectorName => ProjectorName;

    public IReadOnlyCollection<string> ProjectedKinds => BoundaryDecisionWork.ProjectedKinds;

    public EventStreamPattern SourcePattern(Uuid tenantId) =>
        EventStreamPattern.ForPattern(tenantId.ToString(), "boundaries");

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) => base.LoadCheckpointAsync(new CheckpointIdentity(
        ProjectorName, SourcePattern(tenantId)), ct);

    public async ValueTask<long> LoadRevisionAsync(Uuid tenantId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return (await BoundaryDecisionWorkItemDirectorySchema.Revisions.GetAsync(tx,
            RevisionKey, ct).ConfigureAwait(false))?.Revision ?? 0;
    }

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        var (tenantId, boundaryId) = ScopeFor(domainEvent);
        if (tenantId == Uuid.Empty || boundaryId == Uuid.Empty)
            throw new InvalidOperationException(
                "A boundary work event must have complete tenant and boundary scope.");

        var current = await BoundaryDecisionWorkItemDirectorySchema.Boundaries.GetAsync(
            Transaction, boundaryId, ct).ConfigureAwait(false);
        if (current is not null && (current.TenantId != tenantId ||
            current.BoundaryId != boundaryId))
            throw new InvalidOperationException(
                "A boundary work event cannot replace state outside its recorded scope.");

        var boundary = await sourceReader.HydrateAsync(new SystemBoundary(tenantId, boundaryId), ct)
            .ConfigureAwait(false);
        if (!boundary.IsCreated || boundary.ProgramId == Uuid.Empty || boundary.Revision < 1)
            throw new InvalidOperationException(
                "A boundary work event must match its authoritative source aggregate.");

        if (boundary.DraftVersionId == Uuid.Empty)
        {
            if (current is not null)
                await BoundaryDecisionWorkItemDirectorySchema.Boundaries.DeleteAsync(Transaction,
                    current, ct).ConfigureAwait(false);
        }
        else
        {
            if (!boundary.IsVisible || boundary.DraftChangedAt is not { } changedAt ||
                boundary.DraftRevision < 1 || boundary.DraftAuthorMemberId == Uuid.Empty)
                throw new InvalidOperationException(
                    "An open boundary draft must have complete work projection state.");

            var scope = new ResponsibilityScope("boundary", boundaryId,
                boundary.DraftVersionId, boundary.DraftRevision);
            var assignments = boundary.GetResponsibilitySet(scope).ReadAssignments().ToArray();
            var next = new BoundaryDecisionWorkState(tenantId, boundary.ProgramId, boundaryId,
                boundary.Revision, boundary.DraftVersionId, boundary.DraftRevision,
                boundary.DraftAuthorMemberId, changedAt, boundary.LatestReviewOutcome, assignments);
            if (current is null)
                await BoundaryDecisionWorkItemDirectorySchema.Boundaries.InsertAsync(Transaction,
                    next, ct).ConfigureAwait(false);
            else
                await BoundaryDecisionWorkItemDirectorySchema.Boundaries.ReplaceAsync(Transaction,
                    current, next, ct).ConfigureAwait(false);
        }

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
            var page = await BoundaryDecisionWorkItemDirectorySchema.Boundaries.QueryAsync(tx,
                BoundaryDecisionWorkItemDirectorySchema.ByProgram.Query()
                    .WithPrefix(programId.ToString()).Take(200).After(cursor), ct)
                .ConfigureAwait(false);
            foreach (var state in page.Items)
            {
                if (state.TenantId != tenantId || state.ProgramId != programId ||
                    state.BoundaryId == Uuid.Empty || state.SourceRevision < 1)
                    return Result<IReadOnlyList<WorkCandidate>>.Failure(InvalidScope());

                var work = BoundaryDecisionWork.Candidates(state, now, workItemId);
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
        var current = await BoundaryDecisionWorkItemDirectorySchema.Revisions.GetAsync(Transaction,
            RevisionKey, ct).ConfigureAwait(false);
        var next = new AccountableWorkItemProjectionRevision(ProjectorName,
            checked((current?.Revision ?? 0) + 1));
        if (current is null)
            await BoundaryDecisionWorkItemDirectorySchema.Revisions.InsertAsync(Transaction, next, ct)
                .ConfigureAwait(false);
        else
            await BoundaryDecisionWorkItemDirectorySchema.Revisions.ReplaceAsync(Transaction,
                current, next, ct).ConfigureAwait(false);
    }

    static (Uuid TenantId, Uuid BoundaryId) ScopeFor(DomainEvent domainEvent) => domainEvent switch
    {
        BoundaryDraftCreated ev => (ev.TenantId, ev.BoundaryId),
        BoundaryDraftRevised ev => (ev.TenantId, ev.BoundaryId),
        BoundaryDraftDiscarded ev => (ev.TenantId, ev.BoundaryId),
        BoundaryReviewed ev => (ev.TenantId, ev.BoundaryId),
        BoundaryApproved ev => (ev.TenantId, ev.BoundaryId),
        BoundarySuccessorProposed ev => (ev.TenantId, ev.BoundaryId),
        ResponsibilityAssigned ev when ev.Scope.RecordType == "boundary" =>
            (ev.TenantId, ev.Scope.RecordId),
        ResponsibilityRevoked ev when ev.Scope.RecordType == "boundary" =>
            (ev.TenantId, ev.Scope.RecordId),
        _ => (Uuid.Empty, Uuid.Empty),
    };

    static RequestError InvalidScope() => new(RequestErrorKind.Conflict,
        "The boundary work projection has an invalid program scope.");
}

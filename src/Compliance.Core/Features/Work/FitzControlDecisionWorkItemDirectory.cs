using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Responsibilities;
using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

sealed class FitzControlDecisionWorkItemDirectory(IKvClient client,
    IAggregateReader sourceReader, OperatingAuthority authority,
    ControlActivationReleaseGate activationGate, ControlLifecycleReleaseGate lifecycleGate)
    : FitzKvProjectionStore(client,
        "kv://bdgrz/accountable-work-items/control-decisions-v1", ProjectorName),
      IAccountableWorkItemDirectoryReader, IControlDecisionWorkItemProjection
{
    public const string ProjectorName = "AccountableWorkItemControlDecisionV1";
    const string RevisionKey = ProjectorName;

    string IAccountableWorkItemDirectoryReader.ProjectorName => ProjectorName;

    public IReadOnlyCollection<string> ProjectedKinds => ControlDecisionWork.ProjectedKinds;

    public EventStreamPattern SourcePattern(Uuid tenantId) =>
        EventStreamPattern.ForPattern(tenantId.ToString(), "controls");

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) => base.LoadCheckpointAsync(new CheckpointIdentity(
        ProjectorName, SourcePattern(tenantId)), ct);

    public async ValueTask<long> LoadRevisionAsync(Uuid tenantId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return (await ControlDecisionWorkItemDirectorySchema.Revisions.GetAsync(tx,
            RevisionKey, ct).ConfigureAwait(false))?.Revision ?? 0;
    }

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        var (tenantId, controlId) = ScopeFor(domainEvent);
        if (tenantId == Uuid.Empty || controlId == Uuid.Empty)
            return;

        var current = await ControlDecisionWorkItemDirectorySchema.Controls.GetAsync(Transaction,
            controlId, ct).ConfigureAwait(false);
        if (current is not null && (current.TenantId != tenantId ||
                                    current.ControlId != controlId))
            throw new InvalidOperationException(
                "A control decision work event cannot replace state outside its recorded scope.");

        var control = await sourceReader.HydrateAsync(new ControlDraft(tenantId, controlId), ct)
            .ConfigureAwait(false);
        if (!control.IsCreated || control.TenantId != tenantId || control.ProgramId == Uuid.Empty ||
            control.Revision < 1)
            throw new InvalidOperationException(
                "A control decision work event must match its authoritative source aggregate.");

        if (!control.IsVisible)
        {
            if (current is not null)
                await ControlDecisionWorkItemDirectorySchema.Controls.DeleteAsync(Transaction,
                    current, ct).ConfigureAwait(false);
        }
        else
        {
            var identifier = current?.Identifier ?? (domainEvent is ControlDraftCreated created
                ? created.Identifier
                : throw new InvalidOperationException(
                    "A control decision projection must begin with its creation event."));
            var next = ControlDecisionWork.FromSource(tenantId, control, identifier);
            var validation = await ControlDecisionWork.CandidatesAsync(next, authority,
                DateTimeOffset.MinValue, null, activationEnabled: true, lifecycleEnabled: true,
                ct).ConfigureAwait(false);
            if (!validation.IsSuccess)
                throw new InvalidOperationException(validation.Error.Message);

            if (current is null)
                await ControlDecisionWorkItemDirectorySchema.Controls.InsertAsync(Transaction,
                    next, ct).ConfigureAwait(false);
            else
                await ControlDecisionWorkItemDirectorySchema.Controls.ReplaceAsync(Transaction,
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
            var page = await ControlDecisionWorkItemDirectorySchema.Controls.QueryAsync(tx,
                ControlDecisionWorkItemDirectorySchema.ByProgram.Query()
                    .WithPrefix(programId.ToString()).Take(200).After(cursor), ct)
                .ConfigureAwait(false);
            foreach (var state in page.Items)
            {
                if (state.TenantId != tenantId || state.ProgramId != programId ||
                    state.ControlId == Uuid.Empty || state.Revision < 1)
                    return Result<IReadOnlyList<WorkCandidate>>.Failure(InvalidScope());

                var work = await ControlDecisionWork.CandidatesAsync(state, authority, now,
                    workItemId, activationGate.IsEnabled, lifecycleGate.IsEnabled, ct)
                    .ConfigureAwait(false);
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
        var current = await ControlDecisionWorkItemDirectorySchema.Revisions.GetAsync(Transaction,
            RevisionKey, ct).ConfigureAwait(false);
        var next = new AccountableWorkItemProjectionRevision(ProjectorName,
            checked((current?.Revision ?? 0) + 1));
        if (current is null)
            await ControlDecisionWorkItemDirectorySchema.Revisions.InsertAsync(Transaction, next, ct)
                .ConfigureAwait(false);
        else
            await ControlDecisionWorkItemDirectorySchema.Revisions.ReplaceAsync(Transaction,
                current, next, ct).ConfigureAwait(false);
    }

    static (Uuid TenantId, Uuid ControlId) ScopeFor(DomainEvent domainEvent) => domainEvent switch
    {
        ControlDraftCreated ev => (ev.TenantId, ev.ControlId),
        ControlDraftRevised ev => (ev.TenantId, ev.ControlId),
        ControlDraftDiscarded ev => (ev.TenantId, ev.ControlId),
        ControlSuccessorProposed ev => (ev.TenantId, ev.ControlId),
        ControlProposalWithdrawn ev => (ev.TenantId, ev.ControlId),
        ControlReviewed ev => (ev.TenantId, ev.ControlId),
        ControlApproved ev => (ev.TenantId, ev.ControlId),
        ControlRetirementProposed ev => (ev.TenantId, ev.ControlId),
        ControlRetired ev => (ev.TenantId, ev.ControlId),
        ResponsibilityAssigned ev when ev.Scope.RecordType == "control" =>
            (ev.TenantId, ev.Scope.RecordId),
        ResponsibilityRevoked ev when ev.Scope.RecordType == "control" =>
            (ev.TenantId, ev.Scope.RecordId),
        _ => (Uuid.Empty, Uuid.Empty),
    };

    static RequestError InvalidScope() => new(RequestErrorKind.Conflict,
        "The control work projection has an invalid program scope.");
}

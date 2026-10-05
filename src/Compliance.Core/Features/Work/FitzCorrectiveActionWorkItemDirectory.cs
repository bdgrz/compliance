using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

sealed class FitzCorrectiveActionWorkItemDirectory(IKvClient client)
    : FitzKvProjectionStore(client,
        "kv://bdgrz/accountable-work-items/corrective-actions-v1", ProjectorName),
      IAccountableWorkItemDirectoryReader, ICorrectiveActionWorkItemProjection
{
    public const string ProjectorName = "AccountableWorkItemCorrectiveActionV1";
    const string RevisionKey = ProjectorName;

    string IAccountableWorkItemDirectoryReader.ProjectorName => ProjectorName;

    public IReadOnlyCollection<string> ProjectedKinds => [WorkSource.CorrectiveAction];

    public EventStreamPattern SourcePattern(Uuid tenantId) =>
        EventStreamPattern.ForPattern(tenantId.ToString(), "remediation");

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) => base.LoadCheckpointAsync(new CheckpointIdentity(
        ProjectorName, SourcePattern(tenantId)), ct);

    public async ValueTask<long> LoadRevisionAsync(Uuid tenantId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return (await CorrectiveActionWorkItemDirectorySchema.Revisions.GetAsync(tx,
            RevisionKey, ct).ConfigureAwait(false))?.Revision ?? 0;
    }

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case FindingRaised raised:
                await RecordFindingAsync(raised, ct).ConfigureAwait(false);
                break;
            case FindingRevised revised:
                await ReviseFindingAsync(revised, ct).ConfigureAwait(false);
                break;
            case CorrectiveActionAdded added:
                await AddActionAsync(added, ct).ConfigureAwait(false);
                break;
            case CorrectiveActionCompleted completed:
                await CompleteActionAsync(completed, ct).ConfigureAwait(false);
                break;
            case FindingAcceptanceLinked linked:
                await AdvanceFindingAsync(linked.TenantId, linked.ProgramId, linked.FindingId,
                    linked.Revision, ct).ConfigureAwait(false);
                break;
            case FindingClosed closed:
                await CloseFindingAsync(closed, ct).ConfigureAwait(false);
                break;
            case FindingReopened reopened:
                await AdvanceFindingAsync(reopened.TenantId, reopened.ProgramId,
                    reopened.FindingId, reopened.Revision, ct).ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException(
                    $"The corrective action projector cannot apply {domainEvent.GetType().Name}.");
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
            var page = await CorrectiveActionWorkItemDirectorySchema.WorkItems.QueryAsync(tx,
                CorrectiveActionWorkItemDirectorySchema.ByProgram.Query()
                    .WithPrefix(programId.ToString()).Take(200).After(cursor), ct)
                .ConfigureAwait(false);
            foreach (var item in page.Items)
            {
                if (item.TenantId != tenantId || item.ProgramId != programId ||
                    item.Kind != WorkSource.CorrectiveAction || item.FindingId is null ||
                    item.WorkItemId != WorkCandidate.IdFor(item.SourceId,
                        WorkSource.CorrectiveAction))
                    return Result<IReadOnlyList<WorkCandidate>>.Failure(InvalidScope());
                candidates.Add(item.ToCandidate());
            }
            cursor = page.NextCursor;
        } while (cursor is not null);

        return Result<IReadOnlyList<WorkCandidate>>.Success(candidates);
    }

    async ValueTask RecordFindingAsync(FindingRaised raised, CancellationToken ct)
    {
        var finding = new CorrectiveActionFindingContext(raised.TenantId, raised.ProgramId,
            raised.FindingId, raised.Title, raised.Severity, raised.Revision);
        var existing = await CorrectiveActionWorkItemDirectorySchema.FindingContexts.GetAsync(
            Transaction, raised.FindingId, ct).ConfigureAwait(false);
        if (existing is null)
            await CorrectiveActionWorkItemDirectorySchema.FindingContexts.InsertAsync(Transaction,
                finding, ct).ConfigureAwait(false);
        else if (existing != finding)
            throw new InvalidOperationException(
                "A finding cannot change identity or initial state in the work projection.");
    }

    async ValueTask ReviseFindingAsync(FindingRevised revised, CancellationToken ct)
    {
        var context = await AdvanceFindingAsync(revised.TenantId, revised.ProgramId,
            revised.FindingId, revised.Revision, ct, revised.Severity).ConfigureAwait(false);
        string? cursor = null;
        do
        {
            var page = await CorrectiveActionWorkItemDirectorySchema.WorkItems.QueryAsync(
                Transaction, CorrectiveActionWorkItemDirectorySchema.ByFinding.Query()
                    .WithPrefix(revised.FindingId.ToString()).Take(200).After(cursor), ct)
                .ConfigureAwait(false);
            foreach (var existing in page.Items)
            {
                ValidateWorkItem(existing, revised.TenantId, revised.ProgramId,
                    revised.FindingId, existing.SourceId);
                var updated = existing with
                {
                    Materiality = RemediationLedger.Materiality(context.Severity),
                };
                await CorrectiveActionWorkItemDirectorySchema.WorkItems.ReplaceAsync(Transaction,
                    existing, updated, ct).ConfigureAwait(false);
            }
            cursor = page.NextCursor;
        } while (cursor is not null);
    }

    async ValueTask AddActionAsync(CorrectiveActionAdded added, CancellationToken ct)
    {
        var context = await AdvanceFindingAsync(added.TenantId, added.ProgramId, added.FindingId,
            added.Revision, ct).ConfigureAwait(false);
        var action = added.Action;
        if (action.Status != RemediationLedger.Open)
            throw new InvalidOperationException(
                "Only an open corrective action can enter the work projection.");
        var candidate = new WorkCandidate(
            WorkCandidate.IdFor(action.ActionId, WorkSource.CorrectiveAction),
            WorkSource.CorrectiveAction, action.ActionId, null, added.FindingId,
            action.Description, $"Corrective action for finding \"{context.Title}\".",
            action.DueOn, RemediationLedger.Materiality(context.Severity), "complete",
            $"/api/v1/tenants/{added.TenantId}/programs/{added.ProgramId}/" +
            $"findings/{added.FindingId}/corrective-actions/{action.ActionId}/completions",
            new OperatingHolder(OperatingAuthority.MemberHolder, action.OwnerMemberId), null,
            new HashSet<Uuid>(), action.AddedAt);
        var workItem = AccountableWorkItemView.FromCandidate(added.TenantId,
            added.ProgramId, candidate);
        var existing = await CorrectiveActionWorkItemDirectorySchema.WorkItems.GetAsync(
            Transaction, candidate.WorkItemId, ct).ConfigureAwait(false);
        if (existing is null)
            await CorrectiveActionWorkItemDirectorySchema.WorkItems.InsertAsync(Transaction,
                workItem, ct).ConfigureAwait(false);
        else if (existing.TenantId != added.TenantId || existing.ProgramId != added.ProgramId ||
                 existing.FindingId != added.FindingId || existing.SourceId != action.ActionId)
            throw new InvalidOperationException(
                "A corrective action cannot change identity or scope in the work projection.");
        else
            await CorrectiveActionWorkItemDirectorySchema.WorkItems.ReplaceAsync(Transaction,
                existing, workItem, ct).ConfigureAwait(false);
    }

    async ValueTask CompleteActionAsync(CorrectiveActionCompleted completed, CancellationToken ct)
    {
        await AdvanceFindingAsync(completed.TenantId, completed.ProgramId, completed.FindingId,
            completed.Revision, ct).ConfigureAwait(false);
        var workItemId = WorkCandidate.IdFor(completed.ActionId, WorkSource.CorrectiveAction);
        var existing = await CorrectiveActionWorkItemDirectorySchema.WorkItems.GetAsync(
            Transaction, workItemId, ct).ConfigureAwait(false) ?? throw new InvalidOperationException(
            "A corrective action cannot complete before its open event is projected.");
        ValidateWorkItem(existing, completed.TenantId, completed.ProgramId,
            completed.FindingId, completed.ActionId);
        await CorrectiveActionWorkItemDirectorySchema.WorkItems.DeleteAsync(Transaction, existing,
            ct).ConfigureAwait(false);
    }

    async ValueTask CloseFindingAsync(FindingClosed closed, CancellationToken ct)
    {
        await AdvanceFindingAsync(closed.TenantId, closed.ProgramId, closed.FindingId,
            closed.Revision, ct).ConfigureAwait(false);
        string? cursor = null;
        do
        {
            var page = await CorrectiveActionWorkItemDirectorySchema.WorkItems.QueryAsync(
                Transaction, CorrectiveActionWorkItemDirectorySchema.ByFinding.Query()
                    .WithPrefix(closed.FindingId.ToString()).Take(200).After(cursor), ct)
                .ConfigureAwait(false);
            foreach (var item in page.Items)
            {
                ValidateWorkItem(item, closed.TenantId, closed.ProgramId, closed.FindingId,
                    item.SourceId);
                await CorrectiveActionWorkItemDirectorySchema.WorkItems.DeleteAsync(Transaction,
                    item, ct).ConfigureAwait(false);
            }
            cursor = page.NextCursor;
        } while (cursor is not null);
    }

    async ValueTask<CorrectiveActionFindingContext> AdvanceFindingAsync(Uuid tenantId,
        Uuid programId, Uuid findingId, long revision, CancellationToken ct,
        string? severity = null)
    {
        var current = await CorrectiveActionWorkItemDirectorySchema.FindingContexts.GetAsync(
            Transaction, findingId, ct).ConfigureAwait(false) ?? throw new InvalidOperationException(
            "A finding event cannot be projected before its raised event.");
        if (current.TenantId != tenantId || current.ProgramId != programId ||
            revision != current.Revision + 1)
            throw new InvalidOperationException(
                "A finding event cannot change scope or skip a source revision in the work projection.");
        var next = current with { Revision = revision, Severity = severity ?? current.Severity };
        await CorrectiveActionWorkItemDirectorySchema.FindingContexts.ReplaceAsync(Transaction,
            current, next, ct).ConfigureAwait(false);
        return next;
    }

    async ValueTask IncrementRevisionAsync(CancellationToken ct)
    {
        var current = await CorrectiveActionWorkItemDirectorySchema.Revisions.GetAsync(
            Transaction, RevisionKey, ct).ConfigureAwait(false);
        var next = new AccountableWorkItemProjectionRevision(ProjectorName,
            checked((current?.Revision ?? 0) + 1));
        if (current is null)
            await CorrectiveActionWorkItemDirectorySchema.Revisions.InsertAsync(Transaction, next, ct)
                .ConfigureAwait(false);
        else
            await CorrectiveActionWorkItemDirectorySchema.Revisions.ReplaceAsync(Transaction,
                current, next, ct).ConfigureAwait(false);
    }

    static void ValidateWorkItem(AccountableWorkItemView item, Uuid tenantId, Uuid programId,
        Uuid findingId, Uuid actionId)
    {
        if (item.TenantId != tenantId || item.ProgramId != programId ||
            item.FindingId != findingId || item.SourceId != actionId ||
            item.Kind != WorkSource.CorrectiveAction ||
            item.WorkItemId != WorkCandidate.IdFor(actionId, WorkSource.CorrectiveAction))
            throw new InvalidOperationException(
                "A corrective action work item cannot change tenant, program, or source scope.");
    }

    static RequestError InvalidScope() => new(RequestErrorKind.Conflict,
        "The corrective action work projection has an invalid tenant, program, or source scope.");
}

using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

sealed class FitzEvidenceWorkItemDirectory(IKvClient client)
    : FitzKvProjectionStore(client,
        "kv://bdgrz/accountable-work-items/evidence-v1", ProjectorName),
      IEvidenceWorkItemDirectoryReader, IEvidenceWorkItemProjection
{
    public const string ProjectorName = "AccountableWorkItemEvidenceV1";
    const string RevisionKey = ProjectorName;

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) => base.LoadCheckpointAsync(new CheckpointIdentity(
        ProjectorName, EventStreamPattern.ForPattern(tenantId.ToString(), "evidence-requests")), ct);

    public async ValueTask<long> LoadRevisionAsync(Uuid tenantId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return (await EvidenceWorkItemDirectorySchema.Revisions.GetAsync(tx, RevisionKey, ct)
            .ConfigureAwait(false))?.Revision ?? 0;
    }

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case EvidenceRequestOpened opened:
                var candidate = new WorkCandidate(
                    WorkCandidate.IdFor(opened.EvidenceRequestId, WorkSource.EvidenceRequest),
                    WorkSource.EvidenceRequest, opened.EvidenceRequestId, opened.ControlId, null,
                    opened.Title, opened.Instructions, opened.DueOn, null, "fulfil",
                    $"/api/v1/tenants/{opened.TenantId}/programs/{opened.ProgramId}/" +
                    $"evidence-requests/{opened.EvidenceRequestId}/fulfilments",
                    new OperatingHolder(OperatingAuthority.MemberHolder, opened.OwnerMemberId),
                    null, new HashSet<Uuid>(), opened.OpenedAt);
                var workItem = AccountableWorkItemView.FromCandidate(opened.TenantId,
                    opened.ProgramId, candidate);
                var existing = await EvidenceWorkItemDirectorySchema.WorkItems.GetAsync(
                    Transaction, candidate.WorkItemId, ct).ConfigureAwait(false);
                if (existing is null)
                    await EvidenceWorkItemDirectorySchema.WorkItems.InsertAsync(Transaction,
                        workItem, ct).ConfigureAwait(false);
                else if (existing.TenantId != opened.TenantId ||
                         existing.ProgramId != opened.ProgramId)
                    throw new InvalidOperationException(
                        "An evidence work item cannot change tenant or program scope.");
                else
                    await EvidenceWorkItemDirectorySchema.WorkItems.ReplaceAsync(Transaction,
                        existing, workItem, ct).ConfigureAwait(false);
                await IncrementRevisionAsync(ct).ConfigureAwait(false);
                break;
            case EvidenceRequestFulfilled fulfilled:
                await RemoveAsync(fulfilled.TenantId, fulfilled.ProgramId,
                    fulfilled.EvidenceRequestId, ct).ConfigureAwait(false);
                await IncrementRevisionAsync(ct).ConfigureAwait(false);
                break;
            case EvidenceRequestCancelled cancelled:
                await RemoveAsync(cancelled.TenantId, cancelled.ProgramId,
                    cancelled.EvidenceRequestId, ct).ConfigureAwait(false);
                await IncrementRevisionAsync(ct).ConfigureAwait(false);
                break;
        }
    }

    public async ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId,
        Uuid programId, CancellationToken ct = default)
    {
        var candidates = new List<WorkCandidate>();
        string? cursor = null;
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        do
        {
            var page = await EvidenceWorkItemDirectorySchema.WorkItems.QueryAsync(tx,
                EvidenceWorkItemDirectorySchema.ByProgram.Query()
                    .WithPrefix(programId.ToString()).Take(200).After(cursor), ct)
                .ConfigureAwait(false);
            foreach (var item in page.Items)
            {
                if (item.TenantId != tenantId || item.ProgramId != programId ||
                    item.Kind != WorkSource.EvidenceRequest ||
                    item.WorkItemId != WorkCandidate.IdFor(item.SourceId,
                        WorkSource.EvidenceRequest))
                    return Result<IReadOnlyList<WorkCandidate>>.Failure(InvalidScope());
                candidates.Add(item.ToCandidate());
            }
            cursor = page.NextCursor;
        } while (cursor is not null);

        return Result<IReadOnlyList<WorkCandidate>>.Success(candidates);
    }

    async ValueTask RemoveAsync(Uuid tenantId, Uuid programId, Uuid requestId,
        CancellationToken ct)
    {
        var workItemId = WorkCandidate.IdFor(requestId, WorkSource.EvidenceRequest);
        var existing = await EvidenceWorkItemDirectorySchema.WorkItems.GetAsync(Transaction,
            workItemId, ct).ConfigureAwait(false) ?? throw new InvalidOperationException(
            "An evidence work item cannot complete before its open event is projected.");
        if (existing.TenantId != tenantId || existing.ProgramId != programId ||
            existing.WorkItemId != workItemId || existing.Kind != WorkSource.EvidenceRequest ||
            existing.SourceId != requestId)
            throw new InvalidOperationException(
                "An evidence work item cannot be removed outside its source scope.");
        await EvidenceWorkItemDirectorySchema.WorkItems.DeleteAsync(Transaction, existing, ct)
            .ConfigureAwait(false);
    }

    async ValueTask IncrementRevisionAsync(CancellationToken ct)
    {
        var current = await EvidenceWorkItemDirectorySchema.Revisions.GetAsync(Transaction,
            RevisionKey, ct).ConfigureAwait(false);
        var next = new EvidenceWorkItemProjectionRevision(ProjectorName,
            checked((current?.Revision ?? 0) + 1));
        if (current is null)
            await EvidenceWorkItemDirectorySchema.Revisions.InsertAsync(Transaction, next, ct)
                .ConfigureAwait(false);
        else
            await EvidenceWorkItemDirectorySchema.Revisions.ReplaceAsync(Transaction, current,
                next, ct).ConfigureAwait(false);
    }

    static RequestError InvalidScope() => new(RequestErrorKind.Conflict,
        "The evidence work projection has an invalid tenant or program scope.");
}

using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed class FitzActualStaffEngagementLocator(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/actual-staff-engagement-locator/projection-v1",
        ActualStaffEngagementLocatorProjector.WorkloadName),
        IActualStaffEngagementLocatorProjection, IActualStaffEngagementLocatorReader
{
    CheckpointIdentity? _batchIdentity;

    public new ValueTask<IProjectionBatch> BeginAsync(ProjectionBatchContext context, CancellationToken ct = default) =>
        BeginLocatorBatchAsync(context, ct);

    ValueTask<IProjectionBatch> IProjectionStore.BeginAsync(ProjectionBatchContext context, CancellationToken ct) =>
        BeginLocatorBatchAsync(context, ct);

    async ValueTask<IProjectionBatch> BeginLocatorBatchAsync(ProjectionBatchContext context, CancellationToken ct)
    {
        if (!Uuid.TryParse(context.Identity.Pattern.Realm, null, out var tenant) || tenant == Uuid.Empty ||
            context.Identity.Pattern.Area != "client-independence" || context.Identity.Pattern.Resource is not null)
            throw new InvalidOperationException("Actual staff discovery requires its bound owning client workload.");
        var batch = await base.BeginAsync(context, ct).ConfigureAwait(false);
        _batchIdentity = context.Identity;
        return batch;
    }

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity(ActualStaffEngagementLocatorProjector.WorkloadName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "client-independence")), ct);

    public async ValueTask ApplyAsync(DomainEventRecord source, IProjectorContext context, CancellationToken ct)
    {
        if (context.Identity != _batchIdentity ||
            !Uuid.TryParse(context.Identity.Pattern.Realm, null, out var tenant) || tenant == Uuid.Empty ||
            context.Identity.Pattern.Area != "client-independence" ||
            source.Stream != new EventStreamAddress(tenant.ToString(), "client-independence", tenant.ToString()))
            throw new InvalidOperationException("Actual staff discovery requires the exact owning client ledger stream.");
        if (source.Event is not ServiceEngagementAcceptanceRecorded accepted)
            return;
        var view = accepted.Acceptance;
        if (accepted.TenantId != tenant || accepted.RequestId == Uuid.Empty || accepted.ExpectedSequence is < 0 or long.MaxValue ||
            string.IsNullOrWhiteSpace(accepted.Intent) ||
            view is null || view.TenantId != tenant || view.EngagementId == Uuid.Empty || view.Revision != 1 ||
            view.Status != "active" || view.RecordedAt == default || view.Assignments is null ||
            view.Assignments.Count is < 1 or > 1000 || source.NextCursor.Value is null ||
            view.Assignments.Any(staff => staff is null || staff.StaffMemberId == Uuid.Empty || staff.UserId == Uuid.Empty ||
                staff.Practice is not ("attest" or "advisory") || staff.DirectoryStaffRevision <= 0 || !staff.IsCurrent ||
                staff.DirectoryStaffRecordedAt == default || staff.DirectoryStaffRecordedAt > staff.AssignedAt ||
                staff.AssignedAt == default || staff.AssignedAt > view.RecordedAt) ||
            view.Assignments.Select(staff => staff.StaffMemberId).Distinct().Count() != view.Assignments.Count ||
            view.Assignments.Select(staff => staff.UserId).Distinct().Count() != view.Assignments.Count)
            throw new InvalidOperationException("Actual staff discovery requires immutable valid accepted assignment provenance.");
        var digest = IndependenceSourceDigest.Acceptance(view);
        var sourceDigest = IndependenceSourceDigest.AcceptanceEvent(accepted);
        var rows = view.Assignments.Select(staff => new ActualStaffEngagementLocatorView(
            Uuid.CreateVersion5(accepted.RequestId,
                $"actual_staff_locator_v1:{tenant}:{view.EngagementId}:{view.Revision}:{staff.StaffMemberId}:{staff.UserId}"),
            tenant, view.EngagementId, accepted.RequestId, view.Revision, accepted.ExpectedSequence + 1,
            digest, sourceDigest, staff.StaffMemberId, staff.UserId, staff.Practice, staff.DirectoryStaffRevision,
            view.RecordedAt, staff.AssignedAt, source.Stream.Realm, source.Stream.Area, source.Stream.Resource,
            source.ResourceOffset, source.NextCursor.Value)).ToArray();
        // Validate every immutable predecessor before inserting any new row; batch rollback also retains its checkpoint.
        foreach (var row in rows)
            if (await ActualStaffEngagementLocatorSchema.Rows.GetAsync(Transaction, row.LocatorId, ct).ConfigureAwait(false) is { } existing && existing != row)
                throw new InvalidOperationException("An immutable actual staff locator already retains different source facts.");
        foreach (var row in rows)
            if (await ActualStaffEngagementLocatorSchema.Rows.GetAsync(Transaction, row.LocatorId, ct).ConfigureAwait(false) is null)
                await ActualStaffEngagementLocatorSchema.Rows.InsertAsync(Transaction, row, ct).ConfigureAwait(false);
    }

    public async ValueTask<Page<ActualStaffEngagementLocatorView>> ListAsync(Uuid tenantId, int limit,
        string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ActualStaffEngagementLocatorSchema.Rows.QueryAsync(tx,
            ActualStaffEngagementLocatorSchema.ByStaff.Query().Take(Math.Clamp(limit, 1, 200)).After(cursor), ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<Page<ActualStaffEngagementLocatorView>> ListForStaffAsync(Uuid tenantId,
        Uuid staffMemberId, Uuid canonicalUserId, int limit, string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ActualStaffEngagementLocatorSchema.Rows.QueryAsync(tx,
            ActualStaffEngagementLocatorSchema.ByStaff.Query().WithPrefix(staffMemberId.ToString(), canonicalUserId.ToString())
                .Take(Math.Clamp(limit, 1, 200)).After(cursor), ct).ConfigureAwait(false);
    }
}

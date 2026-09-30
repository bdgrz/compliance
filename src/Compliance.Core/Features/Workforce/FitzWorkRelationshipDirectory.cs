using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

sealed class FitzWorkRelationshipDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/work-relationship-directory-v1/projection",
            "WorkRelationshipDirectoryV1"),
        IWorkRelationshipDirectoryReader, IWorkRelationshipDirectoryProjection
{
    public const string ManualSource = "manual";

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity("WorkRelationshipDirectoryV1",
            EventStreamPattern.ForPattern(tenantId.ToString(), "work-relationships")), ct);

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case WorkRelationshipRecorded recorded:
                await WorkRelationshipDirectorySchema.Relationships.InsertAsync(Transaction,
                    new WorkRelationshipView(recorded.TenantId, recorded.RelationshipId, 1,
                        recorded.PersonId, recorded.SourceWorkerId, recorded.Terms.WorkerType,
                        recorded.Terms.LifecycleStatus, recorded.Terms.StartDate,
                        recorded.Terms.EndDate, recorded.Terms.Department,
                        recorded.Terms.ManagerPersonId, recorded.Terms.SponsorPersonId, false,
                        ManualSource, recorded.Actor, recorded.ChangedAt), ct).ConfigureAwait(false);
                break;
            case WorkRelationshipRevised revised:
                var current = await WorkRelationshipDirectorySchema.Relationships.GetAsync(
                    Transaction, revised.RelationshipId, ct).ConfigureAwait(false);
                if (current is null || current.TenantId != revised.TenantId ||
                    current.Revision + 1 != revised.Revision)
                    throw new InvalidOperationException(
                        "A work relationship revision cannot project before its predecessor.");
                await WorkRelationshipDirectorySchema.Relationships.ReplaceAsync(Transaction,
                    current, current with
                    {
                        Revision = revised.Revision,
                        WorkerType = revised.Terms.WorkerType,
                        LifecycleStatus = revised.Terms.LifecycleStatus,
                        StartDate = revised.Terms.StartDate,
                        EndDate = revised.Terms.EndDate,
                        Department = revised.Terms.Department,
                        ManagerPersonId = revised.Terms.ManagerPersonId,
                        SponsorPersonId = revised.Terms.SponsorPersonId,
                        LastChangedBy = revised.Actor,
                        LastChangedAt = revised.ChangedAt,
                    }, ct).ConfigureAwait(false);
                break;
        }
    }

    public async ValueTask<WorkRelationshipView?> GetAsync(Uuid tenantId, Uuid relationshipId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await WorkRelationshipDirectorySchema.Relationships.GetAsync(tx, relationshipId, ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<Page<WorkRelationshipView>> ListAsync(Uuid tenantId, int limit,
        string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await WorkRelationshipDirectorySchema.Relationships.QueryAsync(tx,
            WorkRelationshipDirectorySchema.BySourceWorkerId.Query().Take(Math.Clamp(limit, 1, 200))
                .After(cursor), ct).ConfigureAwait(false);
    }
}

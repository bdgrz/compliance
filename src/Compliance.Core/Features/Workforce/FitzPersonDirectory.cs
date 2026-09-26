using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

sealed class FitzPersonDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/person-directory-v1/projection",
            "PersonDirectoryV1"),
        IPersonDirectoryReader, IPersonDirectoryProjection
{
    public const string ManualSource = "manual";

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity("PersonDirectoryV1",
            EventStreamPattern.ForPattern(tenantId.ToString(), "people")), ct);

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case PersonRecorded recorded:
                await PersonDirectorySchema.People.InsertAsync(Transaction,
                    new PersonView(recorded.TenantId, recorded.PersonId, 1,
                        recorded.DisplayName, recorded.WorkEmail, ManualSource,
                        recorded.Actor, recorded.ChangedAt), ct).ConfigureAwait(false);
                break;
            case PersonRevised revised:
                var current = await PersonDirectorySchema.People.GetAsync(Transaction,
                    revised.PersonId, ct).ConfigureAwait(false);
                if (current is null || current.TenantId != revised.TenantId ||
                    current.Revision + 1 != revised.Revision)
                    throw new InvalidOperationException(
                        "A person revision cannot project before its predecessor.");
                await PersonDirectorySchema.People.ReplaceAsync(Transaction, current,
                    current with
                    {
                        Revision = revised.Revision,
                        DisplayName = revised.DisplayName,
                        WorkEmail = revised.WorkEmail,
                        LastChangedBy = revised.Actor,
                        LastChangedAt = revised.ChangedAt,
                    }, ct).ConfigureAwait(false);
                break;
        }
    }

    public async ValueTask<PersonView?> GetAsync(Uuid tenantId, Uuid personId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await PersonDirectorySchema.People.GetAsync(tx, personId, ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<Page<PersonView>> ListAsync(Uuid tenantId, int limit,
        string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await PersonDirectorySchema.People.QueryAsync(tx,
            PersonDirectorySchema.ByDisplayName.Query().Take(Math.Clamp(limit, 1, 200))
                .After(cursor), ct).ConfigureAwait(false);
    }
}

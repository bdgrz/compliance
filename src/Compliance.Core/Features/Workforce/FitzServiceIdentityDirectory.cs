using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>
///     Projects governed non-human identities. Stored rows never carry ownership state: it depends
///     on the clock and the roster, so readers evaluate it on read.
/// </summary>
sealed class FitzServiceIdentityDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/service-identity-directory-v1/projection",
            "ServiceIdentityDirectoryV1"),
        IServiceIdentityDirectoryReader, IServiceIdentityDirectoryProjection
{
    public const string ManualSource = "manual";

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity("ServiceIdentityDirectoryV1",
            EventStreamPattern.ForPattern(tenantId.ToString(), "service-identities")), ct);

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case ServiceIdentityRecorded recorded:
                var terms = recorded.Terms;
                await ServiceIdentityDirectorySchema.Identities.InsertAsync(Transaction,
                    new ServiceIdentityView(recorded.TenantId, recorded.ServiceIdentityId, 1,
                        terms.DisplayName, terms.IdentityKind, terms.Purpose, terms.Environment,
                        terms.LifecycleStatus, terms.OwnerKind, terms.OwnerId, terms.ReviewBy,
                        ManualSource, false, [], recorded.Actor, recorded.ChangedAt, terms.ExpiresOn), ct)
                    .ConfigureAwait(false);
                break;
            case ServiceIdentityRevised revised:
                var current = await ServiceIdentityDirectorySchema.Identities.GetAsync(Transaction,
                    revised.ServiceIdentityId, ct).ConfigureAwait(false);
                if (current is null || current.TenantId != revised.TenantId ||
                    current.Revision + 1 != revised.Revision)
                    throw new InvalidOperationException(
                        "A service identity revision cannot project before its predecessor.");
                await ServiceIdentityDirectorySchema.Identities.ReplaceAsync(Transaction, current,
                    current with
                    {
                        Revision = revised.Revision,
                        DisplayName = revised.Terms.DisplayName,
                        IdentityKind = revised.Terms.IdentityKind,
                        Purpose = revised.Terms.Purpose,
                        Environment = revised.Terms.Environment,
                        LifecycleStatus = revised.Terms.LifecycleStatus,
                        OwnerKind = revised.Terms.OwnerKind,
                        OwnerId = revised.Terms.OwnerId,
                        ReviewBy = revised.Terms.ReviewBy,
                        ExpiresOn = revised.Terms.ExpiresOn,
                        LastChangedBy = revised.Actor,
                        LastChangedAt = revised.ChangedAt,
                    }, ct).ConfigureAwait(false);
                break;
        }
    }

    public async ValueTask<ServiceIdentityView?> GetAsync(Uuid tenantId, Uuid serviceIdentityId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ServiceIdentityDirectorySchema.Identities.GetAsync(tx, serviceIdentityId, ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<Page<ServiceIdentityView>> ListAsync(Uuid tenantId, int limit,
        string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ServiceIdentityDirectorySchema.Identities.QueryAsync(tx,
            ServiceIdentityDirectorySchema.ByDisplayName.Query().Take(Math.Clamp(limit, 1, 200))
                .After(cursor), ct).ConfigureAwait(false);
    }
}

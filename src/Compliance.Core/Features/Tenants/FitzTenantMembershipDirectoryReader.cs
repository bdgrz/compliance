using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
///     Serves the "TenantMembership" projector: writes join its batch transaction, and query-side
///     reads open their own read-only transaction on the resource it writes for the given tenant.
/// </summary>
sealed class FitzTenantMembershipDirectoryReader
    : FitzKvProjectionStore,
      ITenantMembershipDirectoryProjection,
      ITenantMembershipDirectoryReader
{
    readonly IKvClient _client;

    public FitzTenantMembershipDirectoryReader(IKvClient client)
        : base(client, TenantMembershipDirectoryKeys.Route, "TenantMembership") => _client = client;

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        if (domainEvent is MemberRegistered registered)
        {
            var current = await TenantMembershipDirectorySchema.Directory.GetAsync(Transaction,
                registered.UserId, ct).ConfigureAwait(false);
            if (current is null)
            {
                await TenantMembershipDirectorySchema.Directory.InsertAsync(Transaction,
                    new TenantMembershipView(registered.UserId, registered.TenantId, registered.Affiliation),
                    ct).ConfigureAwait(false);
            }
            else if (current.IsDeprovisioned)
            {
                await TenantMembershipDirectorySchema.Directory.ReplaceAsync(Transaction, current,
                    current with
                    {
                        Affiliation = registered.Affiliation,
                        IsSuspended = false,
                        IsDeprovisioned = false,
                        SuspendedAt = null,
                        SuspendedByMemberId = Uuid.Empty,
                        SuspendedByDisplay = null,
                        SuspensionReason = null,
                        ReinstatedAt = null,
                        ReinstatedByMemberId = Uuid.Empty,
                        ReinstatedByDisplay = null,
                    }, ct).ConfigureAwait(false);
            }
            return;
        }

        switch (domainEvent)
        {
            case MemberSuspended suspended:
                var current = await TenantMembershipDirectorySchema.Directory.GetAsync(Transaction,
                    suspended.UserId, ct).ConfigureAwait(false);
                if (current is null)
                    throw new InvalidOperationException("A member suspension cannot project before registration.");
                await TenantMembershipDirectorySchema.Directory.ReplaceAsync(Transaction, current,
                    current with
                    {
                        IsSuspended = true,
                        SuspendedAt = suspended.SuspendedAt,
                        SuspendedByMemberId = suspended.SuspendedByMemberId,
                        SuspendedByDisplay = suspended.SuspendedByDisplay,
                        SuspensionReason = suspended.Reason,
                        ReinstatedAt = null,
                        ReinstatedByMemberId = Uuid.Empty,
                        ReinstatedByDisplay = null,
                    }, ct).ConfigureAwait(false);
                break;
            case MemberReinstated reinstated:
                var suspendedMembership = await TenantMembershipDirectorySchema.Directory.GetAsync(
                    Transaction, reinstated.UserId, ct).ConfigureAwait(false);
                if (suspendedMembership is null)
                    throw new InvalidOperationException("A member reinstatement cannot project before registration.");
                await TenantMembershipDirectorySchema.Directory.ReplaceAsync(Transaction, suspendedMembership,
                    suspendedMembership with
                    {
                        IsSuspended = false,
                        ReinstatedAt = reinstated.ReinstatedAt,
                        ReinstatedByMemberId = reinstated.ReinstatedByMemberId,
                        ReinstatedByDisplay = reinstated.ReinstatedByDisplay,
                    }, ct).ConfigureAwait(false);
                break;
            case MemberDeprovisioned deprovisioned:
                var active = await TenantMembershipDirectorySchema.Directory.GetAsync(Transaction,
                    deprovisioned.UserId, ct).ConfigureAwait(false);
                if (active is null)
                    throw new InvalidOperationException("A member deprovision cannot project before registration.");
                await TenantMembershipDirectorySchema.Directory.ReplaceAsync(Transaction, active,
                    active with
                    {
                        IsSuspended = false,
                        IsDeprovisioned = true,
                        DeprovisionedAt = deprovisioned.DeprovisionedAt,
                        DeprovisionedByMemberId = deprovisioned.DeprovisionedByMemberId,
                        DeprovisionedByDisplay = deprovisioned.DeprovisionedByDisplay,
                        DeprovisionReason = deprovisioned.Reason,
                    }, ct).ConfigureAwait(false);
                break;
        }
    }

    public async ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId, ct).ConfigureAwait(false);
        return await TenantMembershipDirectorySchema.Directory.GetAsync(tx, userId, ct).ConfigureAwait(false);
    }

    public async ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
        await GetAsync(tenantId, userId, ct).ConfigureAwait(false) is
        { IsSuspended: false, IsDeprovisioned: false };

    public async ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor,
        CancellationToken ct = default)
    {
        // Existing tenant directories predate the by_user index. Backfill from stable primary
        // records in bounded transactions before returning a list, so old memberships are visible
        // without resetting the projector checkpoint. Repeated backfills are idempotent.
        string route;
        await using (var routeTx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false))
            route = routeTx.Route;
        string? backfillCursor = null;
        do
        {
            var batch = await TenantMembershipDirectorySchema.Directory.BackfillAsync(
                _client, route, TenantMembershipDirectorySchema.ByUser,
                200, backfillCursor, ct).ConfigureAwait(false);
            backfillCursor = batch.NextCursor;
        } while (backfillCursor is not null);

        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await TenantMembershipDirectorySchema.Directory.QueryAsync(tx,
            TenantMembershipDirectorySchema.ByUser.Query().Take(limit).After(cursor), ct).ConfigureAwait(false);
    }
}

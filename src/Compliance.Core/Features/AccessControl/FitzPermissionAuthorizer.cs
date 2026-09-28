using System.Text.Json;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Serves the "PermissionProjection" projector: writes join its batch transaction, and
///     query-side reads open their own read-only transaction on the resource it writes for the
///     given tenant.
/// </summary>
sealed class FitzPermissionAuthorizer(IKvClient client, ITenantMembershipDirectoryReader memberships,
    IDomainEventReader events)
    : FitzKvProjectionStore(client, Route, "PermissionProjection"), IPermissionProjection,
      IPermissionAuthorizer, IMemberAccessReader
{
    internal const string Route = "kv://bdgrz/permissions/projection";

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        var stored = await Transaction.GetAsync(PermissionProjectionKeys.State, ct).ConfigureAwait(false);
        var state = stored.Found
            ? JsonSerializer.Deserialize(stored.Value!.Value.Span, ComplianceCoreJsonContext.Default.PermissionProjectionState)
                ?? new PermissionProjectionState()
            : new PermissionProjectionState();
        state.Apply(domainEvent);

        await Transaction.DeleteRangeAsync(
            PermissionProjectionKeys.GrantRangeStart,
            PermissionProjectionKeys.GrantRangeEnd,
            ct).ConfigureAwait(false);
        foreach (var grant in state.Materialize())
        {
            await Transaction.PutAsync(
                PermissionProjectionKeys.Grant(grant.MemberId, grant.Permission),
                "1"u8.ToArray(),
                ct).ConfigureAwait(false);
        }

        var json = JsonSerializer.SerializeToUtf8Bytes(state, ComplianceCoreJsonContext.Default.PermissionProjectionState);
        await Transaction.PutAsync(PermissionProjectionKeys.State, json, ct).ConfigureAwait(false);
    }

    public async ValueTask<bool> IsAllowedAsync(
        Uuid tenantId,
        Uuid userId,
        Uuid memberId,
        string permission,
        CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || userId == Uuid.Empty ||
            memberId != RbacIds.Member(tenantId, userId) || string.IsNullOrWhiteSpace(permission))
            return false;

        // A stale grant key can predate the affiliation-aware projection. Check the current
        // membership before consulting that key so every direct consumer fails closed.
        var membership = await memberships.GetAsync(tenantId.ToString(), userId, ct)
            .ConfigureAwait(false);
        if (membership is not { Affiliation: "client_personnel" } ||
            membership.TenantId != tenantId || membership.UserId != userId)
            return false;
        if (await HasPendingSourceAsync(tenantId, ct).ConfigureAwait(false))
            return false;

        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        var grant = await tx.GetAsync(PermissionProjectionKeys.Grant(memberId, permission), ct).ConfigureAwait(false);
        return grant.Found;
    }

    async ValueTask<bool> HasPendingSourceAsync(Uuid tenantId, CancellationToken ct)
    {
        var pattern = EventStreamPattern.ForPattern(tenantId.ToString());
        var checkpoint = await base.LoadCheckpointAsync(new CheckpointIdentity(
                "PermissionProjection", pattern), ct)
            .ConfigureAwait(false);
        await using var pending = events.ReadAsync(pattern, checkpoint.Cursor, ct).GetAsyncEnumerator(ct);
        while (await pending.MoveNextAsync().ConfigureAwait(false))
        {
            if (PermissionProjector.Handles(pending.Current.Event))
                return true;
        }
        return false;
    }

    public async ValueTask<IReadOnlyList<MemberAccessEdge>> ReadAsync(Uuid tenantId,
        Uuid memberId, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        var stored = await tx.GetAsync(PermissionProjectionKeys.State, ct).ConfigureAwait(false);
        if (!stored.Found)
            return [];
        var state = JsonSerializer.Deserialize(stored.Value!.Value.Span,
            ComplianceCoreJsonContext.Default.PermissionProjectionState);
        return state?.Explain(memberId) ?? [];
    }
}

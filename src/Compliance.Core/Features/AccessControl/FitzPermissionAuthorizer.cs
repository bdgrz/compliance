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
    IDomainEventReader events, IMemberAccessEligibility sourceMember)
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
        permission = Permissions.Normalize(permission);

        // A stale grant key can predate the affiliation-aware projection. Check the current
        // membership before consulting that key so every direct consumer fails closed.
        var membership = await memberships.GetAsync(tenantId.ToString(), userId, ct)
            .ConfigureAwait(false);
        if (membership is not { Affiliation: "client_personnel", IsSuspended: false } ||
            membership.TenantId != tenantId || membership.UserId != userId)
            return false;

        IReadOnlyList<MemberAccessEdge> currentAccess;
        await using (var initialRead = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false))
        {
            var initialGrant = await initialRead.GetAsync(PermissionProjectionKeys.Grant(memberId, permission), ct)
                .ConfigureAwait(false);
            if (!initialGrant.Found)
                return false;
            var stored = await initialRead.GetAsync(PermissionProjectionKeys.State, ct).ConfigureAwait(false);
            if (!stored.Found)
                return false;
            var state = JsonSerializer.Deserialize(stored.Value!.Value.Span,
                ComplianceCoreJsonContext.Default.PermissionProjectionState);
            currentAccess = state?.Explain(memberId) ?? [];
        }

        // Older projections can retain a grant key without an explainable active path.
        // Such a key must never authorize an action, especially while a suspension event
        // is waiting behind the membership and permission projection checkpoints.
        if (!currentAccess.Any(edge => edge.Permissions.Contains(permission, StringComparer.Ordinal)))
            return false;
        if (await HasPendingRevocationAsync(tenantId, memberId, permission, currentAccess, ct)
                .ConfigureAwait(false))
            return false;

        // The projector may have caught up while pending events were scanned. Re-read the
        // materialized key so a completed revocation cannot race past that checkpoint.
        bool currentGrantFound;
        await using (var finalRead = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false))
        {
            var currentGrant = await finalRead.GetAsync(
                PermissionProjectionKeys.Grant(memberId, permission), ct).ConfigureAwait(false);
            currentGrantFound = currentGrant.Found;
        }
        // A suspension can commit after the pending-event scan and before this final key read.
        // Hydrate the tenant-addressed member stream at the last positive decision point.
        return currentGrantFound &&
            await sourceMember.IsEligibleAsync(tenantId, userId, ct).ConfigureAwait(false);
    }

    async ValueTask<bool> HasPendingRevocationAsync(Uuid tenantId, Uuid memberId,
        string permission, IReadOnlyList<MemberAccessEdge> currentAccess, CancellationToken ct)
    {
        var remainingPaths = currentAccess.Where(edge => edge.Permissions.Contains(permission,
            StringComparer.Ordinal)).ToList();
        if (remainingPaths.Count == 0)
            return false;

        var pattern = EventStreamPattern.ForPattern(tenantId.ToString());
        var checkpoint = await base.LoadCheckpointAsync(new CheckpointIdentity(
                "PermissionProjection", pattern), ct)
            .ConfigureAwait(false);
        await using var pending = events.ReadAsync(pattern, checkpoint.Cursor, ct).GetAsyncEnumerator(ct);
        while (await pending.MoveNextAsync().ConfigureAwait(false))
        {
            var domainEvent = pending.Current.Event;
            if (PermissionProjector.RevokesAllMemberAccess(domainEvent, memberId))
                return true;
            remainingPaths.RemoveAll(edge => PermissionProjector.RevokesAccessPath(domainEvent,
                memberId, permission, edge));
            if (remainingPaths.Count == 0)
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

using System.Text.Json;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Serves the "PermissionProjection" projector: writes join its batch transaction, and
///     query-side reads open their own read-only transaction on the resource it writes for the
///     given tenant.
/// </summary>
sealed class FitzPermissionAuthorizer(IKvClient client)
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
        Uuid memberId,
        string permission,
        CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || memberId == Uuid.Empty || string.IsNullOrWhiteSpace(permission))
            return false;

        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        var grant = await tx.GetAsync(PermissionProjectionKeys.Grant(memberId, permission), ct).ConfigureAwait(false);
        return grant.Found;
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

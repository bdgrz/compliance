using System.Text.Json;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed class FitzPermissionProjection(IKvClient client, WorkloadContext workload)
    : FitzKvProjectionStore(client, Route(workload)), IPermissionProjection
{
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

    static string Route(WorkloadContext workload)
    {
        var tenant = workload.Identity.Tenant
            ?? throw new InvalidOperationException("The permission projection requires a tenant workload.");
        return FitzPermissionAuthorizer.Route(tenant.Value);
    }
}

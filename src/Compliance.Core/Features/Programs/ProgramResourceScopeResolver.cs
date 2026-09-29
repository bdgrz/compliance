using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

sealed class ProgramResourceScopeResolver(IProgramDirectoryReader programs,
    IBoundaryDirectoryReader boundaries,
    IClientServiceDirectoryReader services, ISnapshotDirectoryReader snapshots)
    : IProgramResourceScopeResolver
{
    public async ValueTask<bool> IsTenantProgramAsync(Uuid tenantId, Uuid programId,
        CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || programId == Uuid.Empty)
            return false;
        var program = await programs.GetAsync(tenantId, programId, ct).ConfigureAwait(false);
        return program is not null && program.TenantId == tenantId && program.ProgramId == programId;
    }

    public async ValueTask<Uuid?> ResolveProgramIdAsync(Uuid tenantId,
        IProgramResourceRequest request, CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || request.TenantId != tenantId || request.ResourceId == Uuid.Empty ||
            !Enum.IsDefined(request.ResourceKind))
            return null;

        var programId = request.ResourceKind switch
        {
            ProgramResourceKind.Boundary => await ResolveBoundaryAsync(tenantId, request.ResourceId, ct)
                .ConfigureAwait(false),
            ProgramResourceKind.ClientService => await ResolveClientServiceAsync(tenantId,
                    request.ResourceId, ct)
                .ConfigureAwait(false),
            ProgramResourceKind.Snapshot => await ResolveSnapshotAsync(tenantId, request.ResourceId, ct)
                .ConfigureAwait(false),
            _ => null,
        };
        return programId is { } id && await IsTenantProgramAsync(tenantId, id, ct)
                .ConfigureAwait(false)
            ? id
            : null;
    }

    async ValueTask<Uuid?> ResolveBoundaryAsync(Uuid tenantId, Uuid boundaryId,
        CancellationToken ct) =>
        await boundaries.GetAsync(tenantId, boundaryId, ct).ConfigureAwait(false) is
        { TenantId: var foundTenantId, BoundaryId: var foundId, ProgramId: var programId } boundary &&
            boundary.TenantId == tenantId && foundTenantId == tenantId && foundId == boundaryId
            ? programId
            : null;

    async ValueTask<Uuid?> ResolveClientServiceAsync(Uuid tenantId, Uuid serviceId,
        CancellationToken ct) =>
        await services.GetAsync(tenantId, serviceId, ct).ConfigureAwait(false) is
        { TenantId: var foundTenantId, ServiceId: var foundId, ProgramId: { } programId } service &&
            service.TenantId == tenantId && foundTenantId == tenantId && foundId == serviceId
            ? programId
            : null;

    async ValueTask<Uuid?> ResolveSnapshotAsync(Uuid tenantId, Uuid snapshotId,
        CancellationToken ct) =>
        await snapshots.GetAsync(tenantId, snapshotId, ct).ConfigureAwait(false) is
        { TenantId: var foundTenantId, SnapshotId: var foundId, ProgramId: var programId } snapshot &&
            snapshot.TenantId == tenantId && foundTenantId == tenantId && foundId == snapshotId
            ? programId
            : null;
}

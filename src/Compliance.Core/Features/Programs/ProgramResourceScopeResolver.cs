using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

sealed class ProgramResourceScopeResolver(IProgramDirectoryReader programs,
    IBoundaryDirectoryReader boundaries,
    IClientServiceDirectoryReader services, ISnapshotDirectoryReader snapshots,
    IAggregateReader reader)
    : IProgramResourceScopeResolver
{
    public async ValueTask<bool> IsTenantProgramAsync(Uuid tenantId, Uuid programId,
        CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || programId == Uuid.Empty)
            return false;
        var program = await programs.GetAsync(tenantId, programId, ct).ConfigureAwait(false);
        if (program is not null)
            return program.TenantId == tenantId && program.ProgramId == programId;

        // A successful CreateProgram command commits its source event before the directory
        // catches up. The aggregate stream is addressed by both tenant and program ID.
        var source = await reader.HydrateAsync(new ComplianceProgram(tenantId, programId), ct)
            .ConfigureAwait(false);
        return source.IsCreated;
    }

    public async ValueTask<bool> IsTenantApplicationAsync(Uuid tenantId, Uuid applicationId,
        CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || applicationId == Uuid.Empty)
            return false;
        // A preview spans Programs, but the application ID itself must exist in this tenant
        // before an organization-wide grant is evaluated.
        var source = await reader.HydrateApplicationAsync(tenantId, applicationId, ct).ConfigureAwait(false);
        return source.IsCreated;
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
        CancellationToken ct)
    {
        var boundary = await boundaries.GetAsync(tenantId, boundaryId, ct).ConfigureAwait(false);
        if (boundary is not null)
            return boundary.TenantId == tenantId && boundary.BoundaryId == boundaryId
                ? boundary.ProgramId
                : null;
        var source = await reader.HydrateAsync(new SystemBoundary(tenantId, boundaryId), ct)
            .ConfigureAwait(false);
        return source.IsCreated ? source.ProgramId : null;
    }

    async ValueTask<Uuid?> ResolveClientServiceAsync(Uuid tenantId, Uuid serviceId,
        CancellationToken ct)
    {
        var service = await services.GetAsync(tenantId, serviceId, ct).ConfigureAwait(false);
        if (service is not null)
            return service.TenantId == tenantId && service.ServiceId == serviceId
                ? service.ProgramId
                : null;
        var source = await reader.HydrateAsync(new ClientService(tenantId, serviceId), ct)
            .ConfigureAwait(false);
        return source.IsCreated ? source.ProgramId : null;
    }

    async ValueTask<Uuid?> ResolveSnapshotAsync(Uuid tenantId, Uuid snapshotId,
        CancellationToken ct)
    {
        var snapshot = await snapshots.GetAsync(tenantId, snapshotId, ct).ConfigureAwait(false);
        if (snapshot is not null)
            return snapshot.TenantId == tenantId && snapshot.SnapshotId == snapshotId
                ? snapshot.ProgramId
                : null;
        var source = await reader.HydrateAsync(new ImmutableSnapshot(tenantId, snapshotId), ct)
            .ConfigureAwait(false);
        return source.IsFrozen && source.EventTenantId == tenantId &&
            source.EventSnapshotId == snapshotId ? source.ProgramId : null;
    }
}

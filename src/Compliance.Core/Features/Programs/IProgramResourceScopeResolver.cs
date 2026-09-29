using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public interface IProgramResourceScopeResolver
{
    ValueTask<bool> IsTenantProgramAsync(Uuid tenantId, Uuid programId,
        CancellationToken ct = default);

    ValueTask<bool> IsTenantApplicationAsync(Uuid tenantId, Uuid applicationId,
        CancellationToken ct = default);

    ValueTask<Uuid?> ResolveProgramIdAsync(Uuid tenantId, IProgramResourceRequest request,
        CancellationToken ct = default);
}

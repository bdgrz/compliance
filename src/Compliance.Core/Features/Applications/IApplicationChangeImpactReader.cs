using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public interface IApplicationChangeImpactReader
{
    ValueTask<Result<ApplicationChangePreview>> ReadAsync(PreviewApplicationChange request,
        Uuid userId, CancellationToken ct);
}

/// <summary>Trusted worker-only reread of retirement impact; it is never exposed as a transport.</summary>
public interface IApplicationImportRetirementImpactReader
{
    ValueTask<Result<ApplicationChangePreview>> ReadForImportWorkerAsync(
        PreviewApplicationChange request, CancellationToken ct);
}

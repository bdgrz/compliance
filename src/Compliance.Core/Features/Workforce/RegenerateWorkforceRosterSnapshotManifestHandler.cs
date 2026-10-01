using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>Regenerates a frozen roster's package manifest from its retained, verified rows.</summary>
public sealed class RegenerateWorkforceRosterSnapshotManifestHandler(IAggregateReader reader)
    : IRequestHandler<RegenerateWorkforceRosterSnapshotManifest,
        PopulationSnapshotManifestRegeneration>
{
    public ValueTask<Result<PopulationSnapshotManifestRegeneration>> HandleAsync(
        IRequestContext<RegenerateWorkforceRosterSnapshotManifest> context,
        CancellationToken ct) =>
        PopulationSnapshotManifest.RegenerateAsync(reader, context.Request.TenantId,
            context.Request.SnapshotId, WorkforceRosterSnapshotContent.Kind, ct);
}

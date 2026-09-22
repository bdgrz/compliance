using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

public sealed class RegenerateProgramScopeSnapshotManifestHandler(IAggregateReader reader)
    : IRequestHandler<RegenerateProgramScopeSnapshotManifest,
        ProgramScopeSnapshotManifestRegeneration>
{
    public async ValueTask<Result<ProgramScopeSnapshotManifestRegeneration>> HandleAsync(
        IRequestContext<RegenerateProgramScopeSnapshotManifest> context, CancellationToken ct)
    {
        var request = context.Request;
        var source = await reader.HydrateAsync(new ImmutableSnapshot(request.TenantId,
            request.SnapshotId), ct).ConfigureAwait(false);
        if (!source.IsFrozen || source.Id != request.SnapshotId)
            return Failure(RequestErrorKind.NotFound, "The snapshot was not found.");

        var lineage = await SnapshotAmendmentLineage.InspectAsync(reader, source,
            request.TenantId, SnapshotAmendmentLineage.MaximumAmendmentLinks,
            ct).ConfigureAwait(false);
        if (lineage.ExceedsMaximum)
            return Failure(RequestErrorKind.Conflict,
                "The snapshot amendment lineage exceeds the supported depth.");
        if (!lineage.IsConsistent)
            return Failure(RequestErrorKind.Conflict,
                "The snapshot source manifest is inconsistent.");

        var (canonicalManifest, contentSha256) = SnapshotContentIdentity.Manifest(source.Manifest!);
        return Result<ProgramScopeSnapshotManifestRegeneration>.Success(
            new ProgramScopeSnapshotManifestRegeneration(request.TenantId, request.SnapshotId,
                canonicalManifest, contentSha256));
    }

    static Result<ProgramScopeSnapshotManifestRegeneration> Failure(RequestErrorKind kind,
        string message) => Result<ProgramScopeSnapshotManifestRegeneration>.Failure(
            new RequestError(kind, message));
}

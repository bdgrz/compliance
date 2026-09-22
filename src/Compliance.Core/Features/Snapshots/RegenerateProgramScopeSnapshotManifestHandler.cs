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

        var manifest = source.Manifest;
        var hasAmendment = source.AmendsSnapshotId is not null;
        var consistentAmendment = hasAmendment
            ? source.AmendsSnapshotId != Uuid.Empty &&
              source.RootSnapshotId != source.Id &&
              source.AmendsSnapshotId != source.Id &&
              !string.IsNullOrWhiteSpace(source.AmendmentReason) &&
              source.AmendmentReason == source.AmendmentReason.Trim()
            : source.RootSnapshotId == source.Id && source.AmendmentReason is null;
        var sourceIntegrity = source.Kind == "program_scope" &&
            manifest is not null && manifest.TenantId == request.TenantId &&
            source.ProgramId != Uuid.Empty && manifest.ProgramId == source.ProgramId &&
            source.RootSnapshotId != Uuid.Empty && consistentAmendment &&
            SnapshotContentIdentity.MatchesManifest(manifest,
                source.CanonicalManifest ?? string.Empty, source.ContentSha256 ?? string.Empty);
        if (!sourceIntegrity)
            return Failure(RequestErrorKind.Conflict,
                "The snapshot source manifest is inconsistent.");

        var (canonicalManifest, contentSha256) = SnapshotContentIdentity.Manifest(manifest!);
        return Result<ProgramScopeSnapshotManifestRegeneration>.Success(
            new ProgramScopeSnapshotManifestRegeneration(request.TenantId, request.SnapshotId,
                canonicalManifest, contentSha256));
    }

    static Result<ProgramScopeSnapshotManifestRegeneration> Failure(RequestErrorKind kind,
        string message) => Result<ProgramScopeSnapshotManifestRegeneration>.Failure(
        new RequestError(kind, message));
}

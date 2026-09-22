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
        if (!HasConsistentSource(source, request.TenantId))
            return Failure(RequestErrorKind.Conflict,
                "The snapshot source manifest is inconsistent.");

        var ancestor = source;
        var lineage = new HashSet<Uuid> { source.Id };
        while (ancestor.AmendsSnapshotId is { } predecessorId)
        {
            if (!lineage.Add(predecessorId))
                return Failure(RequestErrorKind.Conflict,
                    "The snapshot source manifest is inconsistent.");
            var predecessor = await reader.HydrateAsync(new ImmutableSnapshot(request.TenantId,
                predecessorId), ct).ConfigureAwait(false);
            if (!HasConsistentSource(predecessor, request.TenantId) ||
                predecessor.Id != predecessorId || predecessor.ProgramId != source.ProgramId ||
                predecessor.RootSnapshotId != source.RootSnapshotId)
                return Failure(RequestErrorKind.Conflict,
                    "The snapshot source manifest is inconsistent.");
            ancestor = predecessor;
        }
        if (ancestor.Id != source.RootSnapshotId || ancestor.RootSnapshotId != ancestor.Id)
            return Failure(RequestErrorKind.Conflict,
                "The snapshot source manifest is inconsistent.");

        var (canonicalManifest, contentSha256) = SnapshotContentIdentity.Manifest(manifest!);
        return Result<ProgramScopeSnapshotManifestRegeneration>.Success(
            new ProgramScopeSnapshotManifestRegeneration(request.TenantId, request.SnapshotId,
                canonicalManifest, contentSha256));
    }

    static bool HasConsistentSource(ImmutableSnapshot source, Uuid tenantId)
    {
        var manifest = source.Manifest;
        var hasAmendment = source.AmendsSnapshotId is not null;
        var consistentAmendment = hasAmendment
            ? source.AmendsSnapshotId != Uuid.Empty &&
              source.RootSnapshotId != source.Id &&
              source.AmendsSnapshotId != source.Id &&
              !string.IsNullOrWhiteSpace(source.AmendmentReason) &&
              source.AmendmentReason == source.AmendmentReason.Trim()
            : source.RootSnapshotId == source.Id && source.AmendmentReason is null;
        return source.IsFrozen && source.Version == 1 && source.EventTenantId == tenantId &&
               source.EventSnapshotId == source.Id &&
               source.Kind == "program_scope" && manifest is not null &&
               manifest.FormatVersion == 1 && manifest.TenantId == tenantId &&
               source.ProgramId != Uuid.Empty && manifest.ProgramId == source.ProgramId &&
               manifest.ProgramRevision >= 1 &&
               IsLowercaseSha256(manifest.ProgramContentSha256) &&
               manifest.BoundaryId != Uuid.Empty &&
               manifest.ApprovedBoundaryVersionId != Uuid.Empty &&
               IsLowercaseSha256(manifest.BoundaryContentSha256) &&
               source.RootSnapshotId != Uuid.Empty && consistentAmendment &&
               SnapshotContentIdentity.MatchesManifest(manifest,
                   source.CanonicalManifest ?? string.Empty, source.ContentSha256 ?? string.Empty);
    }

    static bool IsLowercaseSha256(string? value)
    {
        if (value is not { Length: 64 })
            return false;
        foreach (var character in value)
        {
            if ((character < '0' || character > '9') &&
                (character < 'a' || character > 'f'))
                return false;
        }
        return true;
    }

    static Result<ProgramScopeSnapshotManifestRegeneration> Failure(RequestErrorKind kind,
        string message) => Result<ProgramScopeSnapshotManifestRegeneration>.Failure(
        new RequestError(kind, message));
}

using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

static class SnapshotAmendmentLineage
{
    internal const int MaximumAmendmentLinks = 8;

    internal sealed record Inspection(bool IsConsistent, int AmendmentLinks,
        bool ExceedsMaximum);

    internal static async ValueTask<Inspection> InspectAsync(IAggregateReader reader,
        ImmutableSnapshot leaf, Uuid tenantId, int maximumAmendmentLinks,
        CancellationToken ct)
    {
        if (maximumAmendmentLinks < 0 || !HasConsistentSource(leaf, tenantId))
            return new Inspection(false, 0, false);

        var ancestor = leaf;
        var lineage = new HashSet<Uuid> { leaf.Id };
        var amendmentLinks = 0;
        while (ancestor.AmendsSnapshotId is { } predecessorId)
        {
            if (amendmentLinks >= maximumAmendmentLinks)
                return new Inspection(false, amendmentLinks + 1, true);
            if (!lineage.Add(predecessorId))
                return new Inspection(false, amendmentLinks, false);
            amendmentLinks++;
            var predecessor = await reader.HydrateAsync(new ImmutableSnapshot(tenantId,
                predecessorId), ct).ConfigureAwait(false);
            if (!HasConsistentSource(predecessor, tenantId) ||
                predecessor.Id != predecessorId || predecessor.ProgramId != leaf.ProgramId ||
                predecessor.RootSnapshotId != leaf.RootSnapshotId)
                return new Inspection(false, amendmentLinks, false);
            ancestor = predecessor;
        }
        return ancestor.Id == leaf.RootSnapshotId && ancestor.RootSnapshotId == ancestor.Id
            ? new Inspection(true, amendmentLinks, false)
            : new Inspection(false, amendmentLinks, false);
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
               source.EventSnapshotId == source.Id && source.Kind == "program_scope" &&
               manifest is not null && manifest.FormatVersion == 1 &&
               manifest.TenantId == tenantId && source.ProgramId != Uuid.Empty &&
               manifest.ProgramId == source.ProgramId && manifest.ProgramRevision >= 1 &&
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
}

namespace Bdgrz.Compliance.Features.Snapshots;

static class SnapshotManifestImpact
{
    public static IReadOnlyList<string> ChangedSources(ProgramScopeManifest predecessor,
        ProgramScopeManifest amendment)
    {
        if (predecessor.TenantId != amendment.TenantId || predecessor.ProgramId != amendment.ProgramId)
            throw new ArgumentException(
                "Snapshot impact compares manifests of one tenant program only.", nameof(amendment));

        var changed = new List<string>(2);
        if (predecessor.ProgramRevision != amendment.ProgramRevision ||
            !string.Equals(predecessor.ProgramContentSha256, amendment.ProgramContentSha256,
                StringComparison.Ordinal))
            changed.Add("program_revision");
        if (predecessor.BoundaryId != amendment.BoundaryId ||
            predecessor.ApprovedBoundaryVersionId != amendment.ApprovedBoundaryVersionId ||
            !string.Equals(predecessor.BoundaryContentSha256, amendment.BoundaryContentSha256,
                StringComparison.Ordinal))
            changed.Add("approved_boundary_version");
        return changed;
    }
}

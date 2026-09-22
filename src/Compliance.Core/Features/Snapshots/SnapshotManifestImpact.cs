using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

static class SnapshotManifestImpact
{
    public static Result<IReadOnlyList<string>> ChangedSources(ProgramScopeManifest predecessor,
        ProgramScopeManifest amendment)
    {
        if (predecessor.FormatVersion != 1 || amendment.FormatVersion != 1 ||
            predecessor.TenantId != amendment.TenantId || predecessor.ProgramId != amendment.ProgramId)
            return Result<IReadOnlyList<string>>.Failure(new RequestError(RequestErrorKind.Validation,
                "Snapshot impact compares v1 manifests of one tenant program only."));

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
        return Result<IReadOnlyList<string>>.Success(changed);
    }
}

namespace Bdgrz.Compliance.Features.Versioning;

/// <summary>Shared optimistic-version and never-used-draft checks for owning aggregates.</summary>
public static class VersionedRecordRules
{
    public static VersionConflict StaleRevision(string record, long currentRevision) =>
        new(VersionConflictCode.StaleRevision, record, currentRevision);

    public static VersionConflict StaleDraft(string record, Guid currentVersionId,
        long currentRevision) => new(VersionConflictCode.StaleDraft, record,
        currentRevision, currentVersionId);

    public static VersionConflict StaleApprovedVersion(string record, Guid currentVersionId) =>
        new(VersionConflictCode.StaleApprovedVersion, record,
            CurrentVersionId: currentVersionId);

    public static VersionConflict? DraftDiscardConflict(bool everReferenced) =>
        everReferenced
            ? new VersionConflict(VersionConflictCode.ReferencedDraft, "draft")
            : null;
}

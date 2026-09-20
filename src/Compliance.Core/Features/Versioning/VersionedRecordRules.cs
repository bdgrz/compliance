using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Versioning;

/// <summary>Shared optimistic-version and never-used-draft checks for owning aggregates.</summary>
public static class VersionedRecordRules
{
    public static RequestError StaleRevision(string record, long currentRevision) =>
        new(RequestErrorKind.Conflict,
            $"The {record} changed. Current revision: {currentRevision}. Reload it and retry.");

    public static RequestError StaleDraft(string record, Uuid currentVersionId,
        long currentRevision) => new(RequestErrorKind.Conflict,
        $"The {record} draft changed. Current draft version: {currentVersionId}; revision: {currentRevision}.");

    public static RequestError StaleApprovedVersion(string record, Uuid currentVersionId) =>
        new(RequestErrorKind.Conflict,
            $"The {record} has a different approved version. Current approved version: {currentVersionId}.");

    public static RequestError? DraftDiscardConflict(bool everReferenced) =>
        everReferenced
            ? new RequestError(RequestErrorKind.Conflict,
                "A reviewed draft is referenced by an immutable decision and cannot be discarded.")
            : null;
}

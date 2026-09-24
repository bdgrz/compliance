using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Versioning;

/// <summary>Maps Compliance conflict facts at the Portia result boundary.</summary>
public static class VersionConflictRequestErrors
{
    public static RequestError ToRequestError(this VersionConflict conflict) =>
        new(RequestErrorKind.Conflict, conflict.Code switch
        {
            VersionConflictCode.StaleRevision =>
                $"The {conflict.Record} changed. Current revision: {conflict.CurrentRevision}. Reload it and retry.",
            VersionConflictCode.StaleDraft =>
                $"The {conflict.Record} draft changed. Current draft version: {conflict.CurrentVersionId}; revision: {conflict.CurrentRevision}.",
            VersionConflictCode.StaleApprovedVersion =>
                $"The {conflict.Record} has a different approved version. Current approved version: {conflict.CurrentVersionId}.",
            VersionConflictCode.ReferencedDraft =>
                "A reviewed draft is referenced by an immutable decision and cannot be discarded.",
            _ => throw new ArgumentOutOfRangeException(nameof(conflict)),
        });
}

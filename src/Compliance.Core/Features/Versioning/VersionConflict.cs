using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Versioning;

public enum VersionConflictCode
{
    StaleRevision,
    StaleDraft,
    StaleApprovedVersion,
    ReferencedDraft,
}

public sealed record VersionConflict(VersionConflictCode Code, string Record,
    long? CurrentRevision = null, Uuid? CurrentVersionId = null);

namespace Bdgrz.Compliance.Features.Versioning;

public sealed record VersionConflict(VersionConflictCode Code, string Record,
    long? CurrentRevision = null, Guid? CurrentVersionId = null);

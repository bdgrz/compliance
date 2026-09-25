using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Versioning;

public sealed record VersionConflict(VersionConflictCode Code, string Record,
    long? CurrentRevision = null, Uuid? CurrentVersionId = null);

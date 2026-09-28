using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Responsibilities;

public sealed record ResponsibilityScope(string RecordType, Uuid RecordId,
    Uuid VersionId, long Revision);

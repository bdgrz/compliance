using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>The selected as-of revision of a technology record referenced by the program boundary.</summary>
public sealed record ReadinessTechnologyInventoryInput(string SubjectType, Uuid RecordId,
    long? Revision, string? Lifecycle, DateTimeOffset? LastChangedAt);

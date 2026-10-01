namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>Stable source object and immutable source revision; source IDs are case-sensitive.</summary>
public sealed record WorkforceSourceIdentity(string SourceKind, string SourceSystem,
    string SourceRecordId, string SourceRevision);

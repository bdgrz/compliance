using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>A declared concrete application boundary, not an access-review inclusion decision.</summary>
public sealed record SystemInstanceView(Uuid TenantId, Uuid ApplicationId,
    Uuid SystemInstanceId, string Name, string Kind, string? AccessBoundaryReference,
    string SourceKind, string? SourceIdentifier, IReadOnlyList<string> Unresolved,
    Uuid DeclaredByMemberId,
    string DeclaredByDisplay, DateTimeOffset DeclaredAt)
{
    public long Revision { get; init; }
    public long? LegacyApplicationRevision { get; init; }
}

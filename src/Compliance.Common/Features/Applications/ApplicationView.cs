using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>A tenant-authored declaration. References do not establish a verified owner or classification.</summary>
public sealed record ApplicationView(Uuid TenantId, Uuid ApplicationId, long Revision,
    string Name, string Purpose, string? OwnerReference,
    string SourceKind, string SourceIdentifier, bool HasSystemInstances,
    IReadOnlyList<string> Unresolved, Uuid LastChangedByMemberId,
    string LastChangedByDisplay, DateTimeOffset LastChangedAt)
{
    public string? Classification { get; init; }
}

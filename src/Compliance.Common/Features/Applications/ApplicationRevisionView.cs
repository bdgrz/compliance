using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>An immutable tenant-authored application state after one aggregate event.</summary>
public sealed record ApplicationRevisionView(Uuid TenantId, Uuid ApplicationId, long Revision,
    string Name, string Purpose, string? OwnerReference,
    string SourceKind, string SourceIdentifier, bool HasSystemInstances,
    IReadOnlyList<string> Unresolved, Uuid LastChangedByMemberId,
    string LastChangedByDisplay, DateTimeOffset LastChangedAt,
    string ChangeKind, Uuid? SystemInstanceId, SystemInstanceView? SystemInstance)
{
    public string? Classification { get; init; }

    /// <summary>The workforce person accountable for the system (M0-D05).</summary>
    public Uuid? SystemOwnerPersonId { get; init; }

    /// <summary>The workforce person accountable for who has access (M0-D05).</summary>
    public Uuid? AccessOwnerPersonId { get; init; }
}

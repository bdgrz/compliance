using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>A tenant-authored declaration. References do not establish a verified owner or classification.</summary>
public sealed record ApplicationView(Uuid TenantId, Uuid ApplicationId, long Revision,
    string Name, string Purpose, string? OwnerReference,
    string SourceKind, string SourceIdentifier, bool HasSystemInstances,
    IReadOnlyList<string> Unresolved, Uuid LastChangedByMemberId,
    string LastChangedByDisplay, DateTimeOffset LastChangedAt)
{
    readonly ActorReference? _lastChangedBy;

    [JsonPropertyName("last_changed_by")]
    public ActorReference LastChangedBy
    {
        get => _lastChangedBy ?? ActorReference.ForMember(LastChangedByMemberId,
            LastChangedByDisplay);
        init => _lastChangedBy = value;
    }

    public string? Classification { get; init; }

    /// <summary>Whether this application's inventory requires restricted-read authority.</summary>
    public bool IsRestricted { get; init; }

    /// <summary>The workforce person accountable for the system (M0-D05).</summary>
    public Uuid? SystemOwnerPersonId { get; init; }

    /// <summary>The workforce person accountable for who has access (M0-D05).</summary>
    public Uuid? AccessOwnerPersonId { get; init; }

    /// <summary><c>active</c> or <c>retired</c>; retirement never removes history.</summary>
    public string Lifecycle { get; init; } = "active";

    public RetirementView? Retirement { get; init; }
}

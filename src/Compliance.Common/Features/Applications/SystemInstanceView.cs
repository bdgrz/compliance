using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>A declared concrete application boundary, not an access-review inclusion decision.</summary>
public sealed record SystemInstanceView(Uuid TenantId, Uuid ApplicationId,
    Uuid SystemInstanceId, string Name, string Kind, string? AccessBoundaryReference,
    string SourceKind, string? SourceIdentifier, IReadOnlyList<string> Unresolved,
    Uuid DeclaredByMemberId,
    string DeclaredByDisplay, DateTimeOffset DeclaredAt)
{
    readonly ActorReference? _declaredBy;

    [JsonPropertyName("declared_by")]
    public ActorReference DeclaredBy
    {
        get => _declaredBy ?? ActorReference.ForMember(DeclaredByMemberId,
            DeclaredByDisplay);
        init => _declaredBy = value;
    }

    public long Revision { get; init; }
    public long? LegacyApplicationRevision { get; init; }

    /// <summary><c>active</c> or <c>retired</c>; retirement never removes history.</summary>
    public string Lifecycle { get; init; } = "active";

    public RetirementView? Retirement { get; init; }
}

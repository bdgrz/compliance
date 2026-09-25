using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public sealed record RiskDraftView(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    string Identifier, long Revision, string Status, string OwnerResolution,
    RiskDraftContent Content,
    Uuid LastChangedByMemberId, string LastChangedByDisplay, DateTimeOffset LastChangedAt)
{
    readonly ActorReference? _lastChangedBy;

    /// <summary>The snapshotted actor; pre-snapshot rows fall back to the member columns.</summary>
    [JsonPropertyName("last_changed_by")]
    public ActorReference LastChangedBy
    {
        get => _lastChangedBy ?? ActorReference.ForMember(LastChangedByMemberId,
            LastChangedByDisplay);
        init => _lastChangedBy = value;
    }
}

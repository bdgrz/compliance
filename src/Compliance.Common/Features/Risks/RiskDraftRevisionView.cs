using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public sealed record RiskDraftRevisionView(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    string Identifier, long Revision, RiskDraftContent Content,
    Uuid ChangedByMemberId, string ChangedByDisplay, DateTimeOffset ChangedAt)
{
    readonly ActorReference? _actor;

    /// <summary>The snapshotted actor; pre-snapshot rows fall back to the member columns.</summary>
    [JsonPropertyName("actor")]
    public ActorReference Actor
    {
        get => _actor ?? ActorReference.ForMember(ChangedByMemberId, ChangedByDisplay);
        init => _actor = value;
    }
}

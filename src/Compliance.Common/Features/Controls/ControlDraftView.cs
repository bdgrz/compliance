using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public sealed record ControlDraftView(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    string Identifier, long Revision, string Status, string OwnerResolution,
    string ApplicabilityResolution, ControlDraftContent Content,
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

    readonly Uuid? _draftVersionId;

    /// <summary>
    ///     The exact version identity that responsibilities and decisions target. Rows projected
    ///     before successor drafts existed fall back to the initial version line.
    /// </summary>
    [JsonPropertyName("draft_version_id")]
    public Uuid DraftVersionId
    {
        get => _draftVersionId ?? ControlVersionIds.Initial(ControlId);
        init => _draftVersionId = value;
    }
}

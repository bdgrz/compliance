using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public sealed record BoundaryDecisionView(Uuid TenantId, Uuid BoundaryId,
    Uuid DecisionId, Uuid VersionId, long Revision, string Outcome,
    Uuid ActorMemberId, string ActorDisplay, string Rationale,
    DateTimeOffset DecidedAt, Uuid? SupersedesDecisionId,
    Uuid? ReliesOnDecisionId, string? ImpactDigest,
    Uuid? SeparationOfDutiesWaiverId = null)
{
    readonly ActorReference? _actor;

    /// <summary>The recorded decision actor, with a legacy projection fallback.</summary>
    [JsonPropertyName("actor")]
    public ActorReference Actor
    {
        get => _actor ?? ActorReference.ForMember(ActorMemberId, ActorDisplay);
        init => _actor = value;
    }

    /// <summary>Whether this decision used an approved separation-of-duties waiver.</summary>
    [JsonPropertyName("separation_of_duties_waived")]
    public bool SeparationOfDutiesWaived => SeparationOfDutiesWaiverId is not null;
}

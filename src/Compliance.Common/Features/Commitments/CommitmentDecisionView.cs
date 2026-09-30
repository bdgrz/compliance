using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

public sealed record CommitmentDecisionView(Uuid TenantId, Uuid ProgramId, Uuid DraftId,
    Uuid DecisionId, long Revision, string Outcome, string? OwnerReference,
    string? Applicability, string? Interpretation, string? InterpretationNote,
    string Rationale, long? Version, DateOnly? EffectiveFrom, string? ImpactDigest,
    Uuid ActorMemberId, string ActorDisplay, DateTimeOffset DecidedAt,
    Uuid? SeparationOfDutiesWaiverId)
{
    readonly ActorReference? _actor;

    /// <summary>The recorded reviewer.</summary>
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

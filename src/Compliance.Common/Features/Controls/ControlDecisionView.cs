using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>An immutable review or approval decision on one exact control draft revision.</summary>
public sealed record ControlDecisionView(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid DecisionId, Uuid VersionId, long Revision, string Kind, string Outcome,
    ActorReference Actor, string Rationale, DateTimeOffset DecidedAt,
    Uuid? SupersedesDecisionId, Uuid? ReliesOnDecisionId,
    Uuid? SeparationOfDutiesWaiverId)
{
    /// <summary>Whether this decision used an approved separation-of-duties waiver.</summary>
    [JsonPropertyName("separation_of_duties_waived")]
    public bool SeparationOfDutiesWaived => SeparationOfDutiesWaiverId is not null;
}

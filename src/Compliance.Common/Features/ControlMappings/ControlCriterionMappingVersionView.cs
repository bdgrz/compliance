using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>
///     One attributable proposal of a mapping. Status is proposed, accepted, rejected,
///     superseded, or retired; only an accepted version counts toward coverage.
/// </summary>
public sealed record ControlCriterionMappingVersionView(int VersionNumber,
    Uuid ControlVersionId, string Status, string Rationale, string ApplicabilityExplanation,
    ActorReference ProposedBy, DateTimeOffset ProposedAt, Uuid? ReviewDecisionId,
    ActorReference? ReviewedBy, string? ReviewRationale, DateTimeOffset? ReviewedAt,
    Uuid? SeparationOfDutiesWaiverId, ActorReference? RetiredBy, string? RetirementRationale,
    DateTimeOffset? RetiredAt);

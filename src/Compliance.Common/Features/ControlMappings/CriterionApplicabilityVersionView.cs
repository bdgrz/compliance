using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>
///     One attributable not-applicable proposal. Status is proposed, accepted, rejected,
///     superseded, or withdrawn; only an accepted version excludes the criterion from coverage.
/// </summary>
public sealed record CriterionApplicabilityVersionView(int VersionNumber, string Status,
    string Rationale, ActorReference ProposedBy, DateTimeOffset ProposedAt,
    Uuid? ReviewDecisionId, ActorReference? ReviewedBy, string? ReviewRationale,
    DateTimeOffset? ReviewedAt, Uuid? SeparationOfDutiesWaiverId, ActorReference? WithdrawnBy,
    string? WithdrawalRationale, DateTimeOffset? WithdrawnAt);

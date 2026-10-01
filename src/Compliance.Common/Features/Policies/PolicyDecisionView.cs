using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

/// <summary>
///     One recorded decision. <c>Kind</c> is <c>review</c>, <c>approval</c>,
///     <c>periodic_review</c>, or <c>retirement</c>.
/// </summary>
public sealed record PolicyDecisionView(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    Uuid DecisionId, string Kind, string Outcome, long? Revision, long? Version,
    string Rationale, ActorReference Actor, DateTimeOffset DecidedAt,
    Uuid? SeparationOfDutiesWaiverId);

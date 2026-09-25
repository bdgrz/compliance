using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public sealed record BoundaryDecisionView(Uuid TenantId, Uuid BoundaryId,
    Uuid DecisionId, Uuid VersionId, long Revision, string Outcome,
    Uuid ActorMemberId, string ActorDisplay, string Rationale,
    DateTimeOffset DecidedAt, Uuid? SupersedesDecisionId,
    Uuid? ReliesOnDecisionId, string? ImpactDigest);

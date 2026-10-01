using Bdgrz.Compliance.Features.AccessControl;

namespace Bdgrz.Compliance.Features.Policies;

public sealed record PolicyRetirementProposalView(long Version, DateOnly EffectiveUntil,
    string Rationale, ActorReference ProposedBy, DateTimeOffset ProposedAt);

using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

[Discriminator("bdgrz.policy.retirement.proposed", 1)]
public sealed record PolicyRetirementProposed(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    long Revision, long Version, DateOnly EffectiveUntil, string Rationale,
    ActorReference Actor, Uuid ActorMemberId, DateTimeOffset ProposedAt) : DomainEvent;

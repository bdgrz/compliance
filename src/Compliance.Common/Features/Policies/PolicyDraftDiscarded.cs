using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

[Discriminator("bdgrz.policy.draft.discarded", 1)]
public sealed record PolicyDraftDiscarded(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    long Revision, string Rationale, ActorReference Actor, DateTimeOffset DiscardedAt)
    : DomainEvent;

using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

[Discriminator("bdgrz.policy.draft.created", 1)]
public sealed record PolicyDraftCreated(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    Uuid CreateRequestId, string Identifier, PolicyContent Content, string ContentSha256,
    ActorReference Actor, Uuid ActorMemberId, DateTimeOffset ChangedAt) : DomainEvent;

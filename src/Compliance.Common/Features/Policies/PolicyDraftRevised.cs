using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

/// <summary>
///     A new draft revision. <c>PredecessorVersion</c> is set when the revision opens a successor
///     draft from that approved version.
/// </summary>
[Discriminator("bdgrz.policy.draft.revised", 1)]
public sealed record PolicyDraftRevised(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    long Revision, PolicyContent Content, string ContentSha256, long? PredecessorVersion,
    ActorReference Actor, Uuid ActorMemberId, DateTimeOffset ChangedAt) : DomainEvent;

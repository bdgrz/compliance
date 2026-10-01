using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>
///     One evidence request. <c>Status</c> is <c>open</c>, <c>fulfilled</c>, or <c>cancelled</c>; a fulfilled
///     request names the artifact that answered it.
/// </summary>
public sealed record EvidenceRequestView(Uuid TenantId, Uuid ProgramId, Uuid EvidenceRequestId, long Revision,
    string Title, string Instructions, Uuid OwnerMemberId, DateOnly DueOn, Uuid? ControlId, string Status,
    ActorReference RequestedBy, DateTimeOffset OpenedAt, Uuid? ArtifactId, ActorReference? FulfilledBy,
    DateTimeOffset? FulfilledAt, string? CancellationRationale, ActorReference? CancelledBy,
    DateTimeOffset? CancelledAt);

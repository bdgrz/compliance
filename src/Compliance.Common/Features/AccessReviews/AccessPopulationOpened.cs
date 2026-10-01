using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>A population draft was opened for one exact system-instance revision.</summary>
[Discriminator("bdgrz.access_population.opened", 1)]
public sealed record AccessPopulationOpened(Uuid TenantId, Uuid PopulationId,
    Uuid ApplicationId, Uuid SystemInstanceId, long SystemInstanceRevision,
    DateTimeOffset ObservedAt, string SourceKind, string Source, ActorReference OpenedBy,
    DateTimeOffset OpenedAt) : DomainEvent;

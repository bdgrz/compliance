using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

[Discriminator("bdgrz.workforce.observation.resolved", 1)]
public sealed record WorkforceObservationResolved(Uuid TenantId, Uuid ObservationId,
    string Resolution, string Note, ActorReference Actor, DateTimeOffset ResolvedAt) : DomainEvent;

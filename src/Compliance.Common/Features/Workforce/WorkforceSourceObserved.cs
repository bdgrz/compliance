using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

[Discriminator("bdgrz.workforce.source.observed", 1)]
public sealed record WorkforceSourceObserved(Uuid TenantId, Uuid ObservationId,
    WorkforceSourceIdentity Source, string TargetKind, Uuid TargetId, long ObservedTargetRevision,
    WorkforceSourceFacts Facts, DateTimeOffset ObservedAt, ActorReference Actor,
    DateTimeOffset RecordedAt) : DomainEvent;

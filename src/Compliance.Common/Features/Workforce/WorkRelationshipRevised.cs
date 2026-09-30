using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

[Discriminator("bdgrz.workforce.work-relationship.revised", 1)]
public sealed record WorkRelationshipRevised(Uuid TenantId, Uuid RelationshipId, long Revision,
    WorkRelationshipTerms Terms, ActorReference Actor, DateTimeOffset ChangedAt) : DomainEvent;

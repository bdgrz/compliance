using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>A draft's complete observed facts were replaced.</summary>
[Discriminator("bdgrz.access_population.facts_recorded", 1)]
public sealed record AccessPopulationFactsRecorded(Uuid TenantId,
    Uuid PopulationId, long Revision, AccessPopulationFacts Facts, ActorReference RecordedBy,
    DateTimeOffset RecordedAt) : DomainEvent;

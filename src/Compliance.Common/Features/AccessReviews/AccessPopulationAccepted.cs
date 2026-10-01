using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>A population was attested and frozen as an immutable snapshot.</summary>
[Discriminator("bdgrz.access_population.accepted", 1)]
public sealed record AccessPopulationAccepted(Uuid TenantId, Uuid PopulationId,
    long Revision, Uuid SnapshotId, string ContentSha256, Uuid CalculationId, string Attestation,
    ActorReference AcceptedBy, DateTimeOffset AcceptedAt) : DomainEvent;

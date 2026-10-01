using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>An expiring exception for a missing population was approved.</summary>
[Discriminator("bdgrz.access_population.exception_recorded", 1)]
public sealed record AccessPopulationExceptionRecorded(Uuid TenantId,
    Uuid SystemInstanceId, long Revision, AccessPopulationExceptionView Exception) : DomainEvent;

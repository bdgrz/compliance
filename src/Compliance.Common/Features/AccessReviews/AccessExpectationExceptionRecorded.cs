using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>An expiring exception to an approved expectation was approved.</summary>
[Discriminator("bdgrz.access_expectation.exception_recorded", 1)]
public sealed record AccessExpectationExceptionRecorded(Uuid TenantId,
    Uuid SystemInstanceId, long Revision, AccessExpectationExceptionView Exception) : DomainEvent;

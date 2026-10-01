using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>A proposed expectation was approved.</summary>
[Discriminator("bdgrz.access_expectation.approved", 1)]
public sealed record AccessExpectationApproved(Uuid TenantId,
    Uuid SystemInstanceId, long Revision, Uuid ExpectationId, ActorReference ApprovedBy,
    DateTimeOffset ApprovedAt, Uuid? SeparationOfDutiesWaiverId) : DomainEvent;

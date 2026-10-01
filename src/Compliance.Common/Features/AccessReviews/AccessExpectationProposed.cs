using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>An expectation version was proposed.</summary>
[Discriminator("bdgrz.access_expectation.proposed", 1)]
public sealed record AccessExpectationProposed(Uuid TenantId,
    Uuid SystemInstanceId, long Revision, Uuid ProposerMemberId, AccessExpectationView Expectation) : DomainEvent;

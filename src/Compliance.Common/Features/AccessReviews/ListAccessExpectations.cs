using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Reads a system instance's expectations and exceptions with their full history.</summary>
[Discriminator("bdgrz.access_expectation.list", 1)]
public sealed record ListAccessExpectations(Uuid TenantId, Uuid SystemInstanceId)
    : IRequest<AccessExpectationsView>, IAccessReviewRequest, ICallable;

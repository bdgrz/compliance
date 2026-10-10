using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.service-engagement.partner-review-context.get", 1)]
public sealed record GetServiceEngagementPartnerReviewContext(Uuid TenantId, Uuid EngagementId)
    : IRequest<ServiceEngagementPartnerReviewContextView>, IPersonalEngagementPartnerRequest, ICallable;

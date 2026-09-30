using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.system_instance.access_review_scope.get", 1)]
public sealed record GetAccessReviewScope(Uuid TenantId, Uuid ApplicationId,
    Uuid SystemInstanceId, DateTimeOffset? AsOf = null)
    : IRequest<AccessReviewScopeView>, IApplicationInventoryRequest, ICallable;

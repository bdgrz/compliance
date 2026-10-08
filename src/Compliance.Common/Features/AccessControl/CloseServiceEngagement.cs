using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.service-engagement.close", 1)]
public sealed record CloseServiceEngagement(Uuid TenantId, Uuid EngagementId, long ExpectedSequence, string Reason)
    : IRequest<ServiceEngagementView>, IIndependenceAdministrationRequest, ICallable;

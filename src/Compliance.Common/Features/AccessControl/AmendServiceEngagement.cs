using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.service-engagement.amend", 1)]
public sealed record AmendServiceEngagement(Uuid TenantId, Uuid EngagementId, long ExpectedSequence, ServiceEngagementDraftContent Content)
    : IRequest<ServiceEngagementView>, IIndependenceAdministrationRequest, IClientManagementMutationRequest, ICallable;

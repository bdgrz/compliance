using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.service-engagement.create", 1)]
public sealed record CreateServiceEngagement(Uuid TenantId, Uuid EngagementId, long ExpectedSequence, ServiceEngagementDraftContent Content)
    : IRequest<ServiceEngagementView>, IIndependenceAdministrationRequest, IClientManagementMutationRequest, ICallable;

using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.service-engagement.management-acknowledgements.get", 1)]
public sealed record GetEngagementManagementAcknowledgements(Uuid TenantId, Uuid EngagementId)
    : IRequest<IReadOnlyList<EngagementManagementAcknowledgementView>>, IIndependenceAdministrationRequest, ICallable;

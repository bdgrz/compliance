using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

[Discriminator("bdgrz.workforce.service-identity.get", 1)]
public sealed record GetServiceIdentity(Uuid TenantId, Uuid ServiceIdentityId,
    long? MinimumRevision = null) : IRequest<ServiceIdentityView>, IWorkforceRequest, ICallable;

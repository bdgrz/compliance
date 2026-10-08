using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.tenant.member.reinstate", 1)]
public sealed record ReinstateMember(Uuid TenantId, Uuid UserId)
    : IRequest, IClientRbacMutationRequest, ICallable;

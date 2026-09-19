using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

[Discriminator("bdgrz.tenant-invitation.accept", 1)]
public sealed record AcceptTenantInvitation(Uuid TenantId, string EmailAddress, string Token) : IRequest, ICallable;

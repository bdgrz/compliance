using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

[Discriminator("bdgrz.email-address.reserve", 1)]
public sealed record ReserveEmail(Uuid UserId, string EmailAddress) : IRequest, IEmailOwnershipRequest, ICallable;

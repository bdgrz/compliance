using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

[Discriminator("bdgrz.email-address.reserved", 1)]
public sealed record EmailAddressReserved(Uuid UserId, string EmailAddress) : DomainEvent;

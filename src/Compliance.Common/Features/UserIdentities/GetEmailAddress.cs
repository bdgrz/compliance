using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

[Discriminator("bdgrz.email-address.get", 1)]
public sealed record GetEmailAddress(Uuid UserId, string EmailAddress)
    : IRequest<EmailAddressView>, IEmailOwnershipRequest, ICallable;

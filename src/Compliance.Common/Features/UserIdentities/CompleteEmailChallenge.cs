using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

[Discriminator("bdgrz.email-address.complete-challenge", 1)]
public sealed record CompleteEmailChallenge(Uuid UserId, string EmailAddress, string Token)
    : IRequest, IEmailOwnershipRequest, ICallable;

using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

[Discriminator("bdgrz.user-identity.start-recovery", 1)]
public sealed record StartIdentityRecovery(string EmailAddress) : IRequest, ICallable;

using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

[Discriminator("bdgrz.user-identity.complete-recovery", 1)]
public sealed record CompleteIdentityRecovery(
    string EmailAddress,
    Uuid ChallengeId,
    string Token,
    Uuid OldIdentityId) : IRequest, ICallable;

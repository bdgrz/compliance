using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Continues with a deterministic development identity derived from an email address.</summary>
[Discriminator("bdgrz.developer-identity.continue", 1)]
public sealed record ContinueWithDeveloperIdentity(string EmailAddress)
    : IRequest<AuthenticatedUserIdentity>, ICallable;

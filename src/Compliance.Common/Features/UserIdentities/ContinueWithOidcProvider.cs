using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Continues with the authenticated OpenID Connect provider identity.</summary>
[Discriminator("bdgrz.oidc-provider.continue", 1)]
public sealed record ContinueWithOidcProvider : IRequest<AuthenticatedUserIdentity>, ICallable;

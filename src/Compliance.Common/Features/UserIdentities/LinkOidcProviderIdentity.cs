using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Links the authenticated OIDC identity to the current browser user.</summary>
[Discriminator("bdgrz.oidc-provider.link", 1)]
public sealed record LinkOidcProviderIdentity : IRequest<AuthenticatedUserIdentity>, ICallable;

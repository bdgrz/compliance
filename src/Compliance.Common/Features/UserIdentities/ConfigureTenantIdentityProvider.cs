using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Records the configure tenant identity provider intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.user-identities.configure-tenant-identity-provider", 1)]
public sealed record ConfigureTenantIdentityProvider(Uuid TenantId, Uuid ProviderConnectionId, string Issuer, string ClientId, string EncryptedCredentialReference, IReadOnlyList<string> AllowedAudiences) : IRequest, IClientManagementMutationRequest, ICallable;

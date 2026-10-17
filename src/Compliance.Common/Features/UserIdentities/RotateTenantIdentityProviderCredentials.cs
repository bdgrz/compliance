using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Records the rotate tenant identity provider credentials intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.user-identities.rotate-tenant-identity-provider-credentials", 1)]
public sealed record RotateTenantIdentityProviderCredentials(Uuid TenantId, Uuid ProviderConnectionId, string EncryptedCredentialReference, string RotationReference) : IRequest, IClientManagementMutationRequest, ICallable;

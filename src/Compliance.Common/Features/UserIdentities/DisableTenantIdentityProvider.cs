using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Records the disable tenant identity provider intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.user-identities.disable-tenant-identity-provider", 1)]
public sealed record DisableTenantIdentityProvider(Uuid TenantId, Uuid ProviderConnectionId, long ExpectedRevision, string Rationale) : IRequest, IClientManagementMutationRequest, ICallable;

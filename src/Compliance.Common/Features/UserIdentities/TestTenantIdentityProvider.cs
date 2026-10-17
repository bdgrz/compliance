using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Records the test tenant identity provider intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.user-identities.test-tenant-identity-provider", 1)]
public sealed record TestTenantIdentityProvider(Uuid TenantId, Uuid ProviderConnectionId, string TestKey) : IRequest, IClientManagementMutationRequest, ICallable;

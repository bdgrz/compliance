using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

/// <summary>Records the revoke artifact disclosure intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.retention.revoke-artifact-disclosure", 1)]
public sealed record RevokeArtifactDisclosure(Uuid TenantId, Uuid DisclosureId, string Rationale) : IRequest, IClientManagementMutationRequest, ICallable;

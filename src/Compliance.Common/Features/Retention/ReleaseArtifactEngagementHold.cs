using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

/// <summary>Records the release artifact engagement hold intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.retention.release-artifact-engagement-hold", 1)]
public sealed record ReleaseArtifactEngagementHold(Uuid TenantId, Uuid HoldId, string ReleaseAuthority, string Rationale) : IRequest, IClientManagementMutationRequest, ICallable;

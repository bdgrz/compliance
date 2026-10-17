using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

/// <summary>Records the place artifact engagement hold intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.retention.place-artifact-engagement-hold", 1)]
public sealed record PlaceArtifactEngagementHold(Uuid TenantId, Uuid HoldId, Uuid EngagementId, Uuid? ReportArtifactId, string Scope, string Authority, string Reason) : IRequest, IClientManagementMutationRequest, ICallable;

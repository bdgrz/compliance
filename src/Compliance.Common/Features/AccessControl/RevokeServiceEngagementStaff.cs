using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Records the revoke service engagement staff intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.access-control.revoke-service-engagement-staff", 1)]
public sealed record RevokeServiceEngagementStaff(Uuid TenantId, Uuid EngagementId, Uuid MemberId, DateOnly EffectiveOn, string Rationale) : IRequest, IClientManagementMutationRequest, ICallable;

using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Records the assign service engagement staff intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.access-control.assign-service-engagement-staff", 1)]
public sealed record AssignServiceEngagementStaff(Uuid TenantId, Uuid EngagementId, Uuid MemberId, string ServiceRole, DateOnly EffectiveFrom) : IRequest, IClientManagementMutationRequest, ICallable;

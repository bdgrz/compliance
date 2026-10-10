using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.service-engagement.actual-staff.revoke", 1)]
public sealed record RevokeServiceEngagementActualStaff(Uuid TenantId, Uuid EngagementId, Uuid StaffMemberId,
    long ExpectedSequence, string Reason)
    : IRequest<ServiceEngagementAcceptanceView>, IIndependenceAdministrationRequest, IClientManagementMutationRequest, ICallable;

using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.service-engagement.staff.propose", 1)]
public sealed record ProposeServiceEngagementStaff(Uuid TenantId, Uuid EngagementId, Uuid StaffMemberId, long ExpectedSequence)
    : IRequest<ServiceEngagementView>, IIndependenceAdministrationRequest, ICallable;

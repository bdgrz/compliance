using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.service-engagement.staff.withdraw", 1)]
public sealed record WithdrawServiceEngagementStaffProposal(Uuid TenantId, Uuid EngagementId, Uuid StaffMemberId, long ExpectedSequence, string Reason)
    : IRequest<ServiceEngagementView>, IIndependenceAdministrationRequest, ICallable;

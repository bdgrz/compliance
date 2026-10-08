using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.firm-staff.status.set", 1)]
public sealed record SetFirmStaffStatus(Uuid StaffMemberId, bool IsActive, string Reason, long ExpectedSequence)
    : IRequest<FirmStaffMemberView>, IFirmStaffAdministrationRequest, ICallable;

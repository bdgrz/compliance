using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.firm-staff.register", 1)]
public sealed record RegisterFirmStaff(Uuid StaffMemberId, Uuid UserId, string Practice, string SourceReference, long ExpectedSequence)
    : IRequest<FirmStaffMemberView>, IFirmStaffAdministrationRequest, ICallable;

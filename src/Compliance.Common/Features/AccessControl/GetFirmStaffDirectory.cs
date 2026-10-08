using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.firm-staff.list", 1)]
public sealed record GetFirmStaffDirectory
    : IRequest<FirmStaffDirectoryView>, IFirmStaffAdministrationRequest, ICallable;

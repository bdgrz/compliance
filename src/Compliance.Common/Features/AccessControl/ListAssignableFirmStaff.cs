using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.firm-staff.assignable.list", 1)]
public sealed record ListAssignableFirmStaff(Uuid TenantId)
    : IRequest<FirmStaffDirectoryView>, IIndependenceAdministrationRequest, ICallable;

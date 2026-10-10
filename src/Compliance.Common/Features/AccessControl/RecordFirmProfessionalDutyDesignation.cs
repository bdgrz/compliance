using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.firm.professional-duty.designate", 1)]
public sealed record RecordFirmProfessionalDutyDesignation(Uuid DesignationId, Uuid StaffMemberId,
    string Duty, Uuid? TenantId, string SourceReference, long ExpectedSequence)
    : IRequest<FirmProfessionalDutyDesignationView>, IProfessionalDutyAdministrationRequest, ICallable;

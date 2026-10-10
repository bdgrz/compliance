using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.firm.professional-duty.designate", 1)]
public sealed record RecordFirmProfessionalDutyDesignation(Uuid DesignationId, string SignedAssertion,
    long ExpectedSequence)
    : IRequest<FirmProfessionalDutyDesignationView>, IProfessionalDutyAdministrationRequest, ICallable;

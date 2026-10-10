using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.firm.professional-duty.revoke", 1)]
public sealed record RevokeFirmProfessionalDutyDesignation(Uuid DesignationId, long ExpectedSequence,
    string Reason) : IRequest<FirmProfessionalDutyDesignationView>, IProfessionalDutyAdministrationRequest, ICallable;

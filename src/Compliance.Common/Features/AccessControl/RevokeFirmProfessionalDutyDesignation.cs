using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.firm.professional-duty.revoke", 1)]
public sealed record RevokeFirmProfessionalDutyDesignation(Uuid DesignationId, string SignedAssertion,
    long ExpectedSequence) : IRequest<FirmProfessionalDutyDesignationView>, IProfessionalDutyAdministrationRequest, ICallable;

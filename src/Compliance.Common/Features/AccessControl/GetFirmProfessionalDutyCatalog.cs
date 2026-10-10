using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.firm.professional-duty.catalog.get", 1)]
public sealed record GetFirmProfessionalDutyCatalog
    : IRequest<FirmProfessionalDutyCatalogView>, IProfessionalDutyAdministrationRequest, ICallable;

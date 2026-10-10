using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

[Discriminator("bdgrz.firm.professional-duty.designation-recorded", 1)]
public sealed record FirmProfessionalDutyDesignationRecorded(Uuid RequestId, long ExpectedSequence,
    FirmProfessionalDutyDesignationView Designation) : DomainEvent;

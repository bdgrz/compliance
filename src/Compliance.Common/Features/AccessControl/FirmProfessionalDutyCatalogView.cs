namespace Bdgrz.Compliance.Features.AccessControl;

public sealed record FirmProfessionalDutyCatalogView(long Sequence,
    IReadOnlyList<FirmProfessionalDutyDesignationView> Designations);

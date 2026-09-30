using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

public sealed record ControlCriterionMappingRegistration(Uuid MappingId, long Revision,
    int VersionNumber);

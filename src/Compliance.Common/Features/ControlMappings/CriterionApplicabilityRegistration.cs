using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

public sealed record CriterionApplicabilityRegistration(Uuid DecisionId, long Revision,
    int VersionNumber);

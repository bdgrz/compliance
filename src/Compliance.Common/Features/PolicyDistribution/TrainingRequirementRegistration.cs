using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

public sealed record TrainingRequirementRegistration(Uuid RequirementId, string Identifier,
    long Version);

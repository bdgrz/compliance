using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>Reads the latest version, or the exact <c>Version</c> when given.</summary>
[Discriminator("bdgrz.training_requirement.get", 1)]
public sealed record GetTrainingRequirement(Uuid TenantId, Uuid ProgramId,
    Uuid RequirementId, long? Version = null)
    : IRequest<TrainingRequirementView>, IProgramReadRequest, ICallable;

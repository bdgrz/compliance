using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>Defines an immutable successor version; launched campaigns keep their version.</summary>
[Discriminator("bdgrz.training_requirement.revise", 1)]
public sealed record ReviseTrainingRequirement(Uuid TenantId, Uuid ProgramId,
    Uuid RequirementId, long ExpectedVersion, TrainingRequirementContent Content)
    : IRequest<TrainingRequirementRegistration>, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;

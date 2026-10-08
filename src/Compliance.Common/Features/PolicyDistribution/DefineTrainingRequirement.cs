using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

[Discriminator("bdgrz.training_requirement.define", 1)]
public sealed record DefineTrainingRequirement(Uuid TenantId, Uuid ProgramId,
    string Identifier, TrainingRequirementContent Content)
    : IRequest<TrainingRequirementRegistration>, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;

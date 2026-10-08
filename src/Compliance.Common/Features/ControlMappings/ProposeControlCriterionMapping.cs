using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>
///     Proposes that an exact approved control version addresses one criterion or point of focus
///     in the program's selected edition. ExpectedRevision is 0 for a new mapping.
/// </summary>
[Discriminator("bdgrz.control_mapping.propose", 1)]
public sealed record ProposeControlCriterionMapping(Uuid TenantId, Uuid ProgramId,
    Uuid ControlId, Uuid ControlVersionId, Uuid EditionId, string CriterionIdentifier,
    long ExpectedRevision, string Rationale, string ApplicabilityExplanation)
    : IRequest<ControlCriterionMappingRegistration>, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;

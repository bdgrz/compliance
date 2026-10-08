using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>
///     Proposes that one criterion of the program's selected edition does not apply. It counts
///     only after an independent review accepts it. ExpectedRevision is 0 for a new decision.
///     HTTP-only.
/// </summary>
[Discriminator("bdgrz.criterion_applicability.propose_not_applicable", 1)]
public sealed record ProposeCriterionNotApplicable(Uuid TenantId, Uuid ProgramId,
    Uuid EditionId, string CriterionIdentifier, long ExpectedRevision, string Rationale)
    : IRequest<CriterionApplicabilityRegistration>, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;

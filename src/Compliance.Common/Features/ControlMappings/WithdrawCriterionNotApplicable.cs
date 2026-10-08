using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>Restores an accepted not-applicable criterion to coverage, keeping its history.</summary>
[Discriminator("bdgrz.criterion_applicability.withdraw_not_applicable", 1)]
public sealed record WithdrawCriterionNotApplicable(Uuid TenantId, Uuid ProgramId,
    Uuid DecisionId, long ExpectedRevision, string Rationale)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;

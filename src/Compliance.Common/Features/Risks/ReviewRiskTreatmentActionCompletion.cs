using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     Independently accepts or rejects the pending completion of a treatment action. The
///     submitter and the accountable member may review only under an approved waiver scoped to
///     (risk_treatment_action, action_id, submission_id, expected_revision, review). HTTP-only.
/// </summary>
[Discriminator("bdgrz.risk.treatment_action.completion.review", 1)]
public sealed record ReviewRiskTreatmentActionCompletion(Uuid TenantId, Uuid ProgramId,
    Uuid RiskId, Uuid ActionId, long ExpectedRevision, string Outcome, string Rationale,
    Uuid? SeparationOfDutiesWaiverId = null) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;

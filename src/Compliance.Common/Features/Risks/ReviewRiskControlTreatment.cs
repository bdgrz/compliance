using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     Independently accepts or rejects a proposed control treatment assertion; HTTP-only. The
///     proposer may review only under an approved waiver scoped to
///     (risk_control_treatment, treatment_id, control_version_id, expected_revision, review).
/// </summary>
[Discriminator("bdgrz.risk.control_treatment.review", 1)]
public sealed record ReviewRiskControlTreatment(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    Uuid TreatmentId, long ExpectedRevision, string Outcome, string Rationale,
    Uuid? SeparationOfDutiesWaiverId = null) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;

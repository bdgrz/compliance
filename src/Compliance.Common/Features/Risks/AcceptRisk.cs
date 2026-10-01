using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>
///     A personal acceptance by the acting member; HTTP-only, never an MCP tool. A risk owner
///     may accept their own risk only under an approved waiver scoped to
///     (risk, risk_id, residual_assessment_id, expected_revision, approve).
/// </summary>
[Discriminator("bdgrz.risk.accept", 1)]
public sealed record AcceptRisk(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long ExpectedRevision, Uuid ResidualAssessmentId, string ApproverAuthority,
    DateTimeOffset ExpiresAt, string Rationale, Uuid? SeparationOfDutiesWaiverId = null)
    : IRequest<RiskAcceptanceView>, IProgramScopedRequest, ICallable;

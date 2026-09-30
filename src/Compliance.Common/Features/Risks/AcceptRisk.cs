using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>A personal acceptance by the acting member; HTTP-only, never an MCP tool.</summary>
[Discriminator("bdgrz.risk.accept", 1)]
public sealed record AcceptRisk(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long ExpectedRevision, Uuid ResidualAssessmentId, string ApproverAuthority,
    DateTimeOffset ExpiresAt, string Rationale)
    : IRequest<RiskAcceptanceView>, IProgramScopedRequest, ICallable;

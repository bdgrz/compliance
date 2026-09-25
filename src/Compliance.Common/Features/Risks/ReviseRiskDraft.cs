using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

[Discriminator("bdgrz.risk.draft.revise", 1)]
public sealed record ReviseRiskDraft(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long ExpectedRevision, string Title, string Scenario, string PotentialEffect,
    string? SourceNote = null) : IRequest,
    IProgramManagementRequest, ICallable;

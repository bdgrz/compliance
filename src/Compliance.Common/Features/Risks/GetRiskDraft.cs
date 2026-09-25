using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

[Discriminator("bdgrz.risk.draft.get", 1)]
public sealed record GetRiskDraft(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long? MinimumRevision = null) : IRequest<RiskDraftView>, IProgramManagementRequest, ICallable;

using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

[Discriminator("bdgrz.risk.draft.revision.get", 1)]
public sealed record GetRiskDraftRevision(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long Revision) : IRequest<RiskDraftRevisionView>, IProgramManagementRequest, ICallable;

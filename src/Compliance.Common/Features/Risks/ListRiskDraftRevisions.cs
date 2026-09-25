using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

[Discriminator("bdgrz.risk.draft.revisions.list", 1)]
public sealed record ListRiskDraftRevisions(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    int? Limit = null, string? Cursor = null, long? MinimumRiskRevision = null)
    : IRequest<Page<RiskDraftRevisionView>>, IProgramManagementRequest, ICallable;

using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

[Discriminator("bdgrz.risk.draft.list", 1)]
public sealed record ListRiskDrafts(Uuid TenantId, Uuid ProgramId, int? Limit = null,
    string? Cursor = null) : IRequest<Page<RiskDraftView>>, IProgramManagementRequest, ICallable;

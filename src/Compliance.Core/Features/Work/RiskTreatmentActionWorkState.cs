using Bdgrz.Compliance.Features.Risks;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public sealed record RiskTreatmentActionWorkState(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    RiskTreatmentActionView Action, Uuid? PendingSubmitterMemberId);

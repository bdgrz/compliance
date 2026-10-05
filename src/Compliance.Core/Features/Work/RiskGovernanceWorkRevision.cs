using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public sealed record RiskGovernanceWorkRevision(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long Revision);

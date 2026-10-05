using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public sealed record PolicyDecisionWorkState(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    long SourceRevision, AccountableWorkItemView[] Items);

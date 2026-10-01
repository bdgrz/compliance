using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

[Discriminator("bdgrz.finding.reopen", 1)]
public sealed record ReopenFinding(Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    long ExpectedRevision, string Reason)
    : IRequest<FindingView>, IProgramScopedRequest, ICallable;

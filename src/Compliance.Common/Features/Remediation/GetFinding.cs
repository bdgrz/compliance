using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

[Discriminator("bdgrz.finding.get", 1)]
public sealed record GetFinding(Uuid TenantId, Uuid ProgramId, Uuid FindingId)
    : IRequest<FindingView>, IProgramReadRequest, ICallable;

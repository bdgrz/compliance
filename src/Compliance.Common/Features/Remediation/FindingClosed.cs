using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

[Discriminator("bdgrz.finding.closed", 1)]
public sealed record FindingClosed(Uuid TenantId, Uuid ProgramId, Uuid FindingId, long Revision,
    FindingClosureView Closure) : DomainEvent;

using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

[Discriminator("bdgrz.finding.corrective_action.added", 1)]
public sealed record CorrectiveActionAdded(Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    long Revision, CorrectiveActionView Action) : DomainEvent;

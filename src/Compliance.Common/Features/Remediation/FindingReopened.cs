using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

[Discriminator("bdgrz.finding.reopened", 1)]
public sealed record FindingReopened(Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    long Revision, string Reason, ActorReference ReopenedBy, DateTimeOffset ReopenedAt)
    : DomainEvent;

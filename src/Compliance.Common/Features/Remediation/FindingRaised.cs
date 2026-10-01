using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

[Discriminator("bdgrz.finding.raised", 1)]
public sealed record FindingRaised(Uuid TenantId, Uuid ProgramId, Uuid FindingId, long Revision,
    FindingSource Source, string Title, string Description, string Severity,
    string AffectedScope, Uuid OwnerMemberId, DateOnly DueOn, IReadOnlyList<FindingLink> Links,
    ActorReference RaisedBy, DateTimeOffset RaisedAt) : DomainEvent;

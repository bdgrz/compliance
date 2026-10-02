using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

[Discriminator("bdgrz.provider.assurance_report.revised", 1)]
public sealed record AssuranceReportRevised(Uuid TenantId, Uuid ReportId, Uuid ProviderId, Uuid RequestId,
    long Revision, AssuranceReportContent Content, long ProviderRevision, ActorReference Actor,
    DateTimeOffset RecordedAt) : DomainEvent;

using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

[Discriminator("bdgrz.provider.assurance_report.recorded", 1)]
public sealed record AssuranceReportRecorded(Uuid TenantId, Uuid ReportId, Uuid ProviderId, Uuid RequestId,
    AssuranceReportContent Content, long ProviderRevision, ActorReference Actor, DateTimeOffset RecordedAt) : DomainEvent;

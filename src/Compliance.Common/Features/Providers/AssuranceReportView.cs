using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
///     One retained assurance report revision. A reader without restricted authority receives
///     <c>Redacted</c> content: exception, control and gap text and the citation are withheld, while
///     the counts, kind, issuer, period and opinion stay shareable.
/// </summary>
public sealed record AssuranceReportView(Uuid TenantId, Uuid ReportId, Uuid ProviderId, long Revision,
    AssuranceReportContent Content, long ProviderRevision, int ExceptionCount,
    int ComplementaryControlCount, int CoverageGapCount, ActorReference RecordedBy,
    DateTimeOffset RecordedAt, bool Redacted = false);

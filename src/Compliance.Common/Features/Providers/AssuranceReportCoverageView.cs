using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
///     <c>Currency</c> is <c>current</c> or <c>stale</c>; <c>PeriodCoverage</c> is
///     <c>covers_as_of</c>, <c>bridged_only</c>, <c>bridge_expired</c>, <c>uncovered_after_period</c>
///     or <c>not_yet_effective</c>. A bridged-only, stale or lapsed report is never <c>Complete</c>.
/// </summary>
public sealed record AssuranceReportCoverageView(Uuid ReportId, long Revision, string ReportKind, string Issuer,
    DateOnly? PeriodStart, DateOnly PeriodEnd, string? Opinion, int ExceptionCount, int CoverageGapCount,
    DateOnly? BridgeCoversThrough, string Currency, string PeriodCoverage, bool Complete,
    IReadOnlyList<string> IncompleteReasons);

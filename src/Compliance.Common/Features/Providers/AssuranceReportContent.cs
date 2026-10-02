namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
///     Authored facts about one assurance report or questionnaire (M0-D11). Point-in-time records
///     omit <c>PeriodStart</c> and use <c>PeriodEnd</c> as the report date. An absent opinion means
///     none was issued; nothing is inferred. A citation must explicitly classify its metadata; an
///     unclassified report is treated as restricted.
/// </summary>
public sealed record AssuranceReportContent(string ReportKind, string Issuer, string Scope,
    DateOnly PeriodEnd, DateOnly? PeriodStart = null, string? Opinion = null, string? OpinionSource = null,
    IReadOnlyList<string>? CoveredServices = null, IReadOnlyList<AssuranceExceptionNote>? Exceptions = null,
    IReadOnlyList<string>? ComplementaryControls = null, IReadOnlyList<string>? CoverageGaps = null,
    AssuranceBridgeLetter? BridgeLetter = null, ProviderSourceCitation? Citation = null);

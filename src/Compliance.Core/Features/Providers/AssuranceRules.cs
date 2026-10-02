using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
///     M0-D11 assurance-report and due-diligence rules. The only cadence is the decided annual
///     review; no firm-specific threshold, opinion judgment or trust-services mapping is inferred.
/// </summary>
public static class AssuranceRules
{
    public const int AnnualMonths = 12;
    public const int MaximumListItems = 50;
    static readonly DateOnly EarliestDate = new(1990, 1, 1);
    static readonly DateOnly LatestDate = new(2200, 1, 1);
    static readonly string[] Kinds = ["soc2_type1", "soc2_type2", "iso27001", "questionnaire"];
    static readonly string[] Classifications = ["public", "internal", "confidential", "restricted"];

    public static string? InputError(AssuranceReportContent? content) =>
        content is null || content.Exceptions?.Any(static item => item is null) == true ||
        content.CoveredServices?.Any(static item => item is null) == true ||
        content.ComplementaryControls?.Any(static item => item is null) == true ||
        content.CoverageGaps?.Any(static item => item is null) == true
            ? "An assurance report requires content with non-null collection entries."
            : null;

    public static string? InputError(ProviderReviewContent? content) =>
        content is null || content.Exceptions?.Any(static item => item is null) == true
            ? "A provider review requires content with non-null collection entries."
            : null;

    public static AssuranceReportContent Normalize(AssuranceReportContent content) => content with
    {
        ReportKind = content.ReportKind?.Trim() ?? string.Empty,
        Issuer = content.Issuer?.Trim() ?? string.Empty,
        Scope = content.Scope?.Trim() ?? string.Empty,
        Opinion = Optional(content.Opinion),
        OpinionSource = Optional(content.OpinionSource),
        CoveredServices = List(content.CoveredServices),
        Exceptions = Array.AsReadOnly((content.Exceptions ?? []).Select(static item => item with
        {
            Reference = Optional(item.Reference),
            Description = item.Description?.Trim() ?? string.Empty,
        }).ToArray()),
        ComplementaryControls = List(content.ComplementaryControls),
        CoverageGaps = List(content.CoverageGaps),
        Citation = Normalize(content.Citation),
    };

    public static ProviderReviewContent Normalize(ProviderReviewContent content) => content with
    {
        EvidenceKind = content.EvidenceKind?.Trim() ?? string.Empty,
        Conclusion = content.Conclusion?.Trim() ?? string.Empty,
        Rationale = content.Rationale?.Trim() ?? string.Empty,
        Exceptions = List(content.Exceptions),
        Evidence = Normalize(content.Evidence),
    };

    public static string? Validate(AssuranceReportContent content)
    {
        if (!Kinds.Contains(content.ReportKind, StringComparer.Ordinal))
            return "The report kind must be soc2_type1, soc2_type2, iso27001 or questionnaire.";
        if (content.Issuer.Length is < 1 or > 200 || content.Scope.Length is < 1 or > 2000)
            return "A report requires its exact issuer (at most 200 characters) and scope (at most 2000).";
        if (!InRange(content.PeriodEnd) || content.PeriodStart is { } start && (!InRange(start) || start > content.PeriodEnd))
            return "The report period must be a valid range; a point-in-time record carries only its date.";
        if (content.ReportKind == "soc2_type2" ? content.PeriodStart is null : content.PeriodStart is not null)
            return "Only a SOC 2 Type 2 report has a period start; other kinds record a point-in-time date.";
        if (content.ReportKind.StartsWith("soc2", StringComparison.Ordinal) ? content.Opinion is null : content.Opinion is not null)
            return "A SOC 2 report records the issuer's opinion; ISO certificates and questionnaires issue none.";
        if (content.Opinion is not (null or "unqualified" or "qualified" or "adverse" or "disclaimer"))
            return "The opinion must be unqualified, qualified, adverse or disclaimer.";
        if (content.Opinion is null ? content.OpinionSource is not null : content.OpinionSource is not { Length: <= 500 })
            return "An opinion records its source (at most 500 characters); no source is allowed without one.";
        if (content.CoveredServices!.Count > MaximumListItems || content.Exceptions!.Count > MaximumListItems ||
            content.ComplementaryControls!.Count > MaximumListItems || content.CoverageGaps!.Count > MaximumListItems ||
            content.CoveredServices.Any(static item => item.Length is < 1 or > 500) ||
            content.ComplementaryControls.Any(static item => item.Length is < 1 or > 2000) ||
            content.CoverageGaps.Any(static item => item.Length is < 1 or > 2000) ||
            content.Exceptions.Any(static item => item.Description.Length is < 1 or > 2000 || item.Reference is { Length: > 200 }))
            return "Report services, exceptions, complementary controls and gaps are bounded to 50 entries of nonempty text.";
        if (content.BridgeLetter is { } bridge && (!InRange(bridge.LetterDate) || !InRange(bridge.CoversFrom) ||
                !InRange(bridge.CoversThrough) || bridge.CoversFrom <= content.PeriodEnd ||
                bridge.CoversThrough < bridge.CoversFrom || bridge.LetterDate < content.PeriodEnd))
            return "A bridge letter covers an interval after the report period and is dated on or after it.";
        return CitationError(content.Citation);
    }

    public static string? Validate(ProviderReviewContent content)
    {
        if (!InRange(content.ReviewedAt) || !InRange(content.NextReviewDue) || content.NextReviewDue <= content.ReviewedAt)
            return "A review requires valid dates with the next review due after the review.";
        if (!Kinds.Contains(content.EvidenceKind, StringComparer.Ordinal))
            return "The evidence kind must be soc2_type1, soc2_type2, iso27001 or questionnaire.";
        if (content.Conclusion is not ("acceptable" or "acceptable_with_exceptions" or "not_acceptable"))
            return "The conclusion must be acceptable, acceptable_with_exceptions or not_acceptable.";
        if (content.Rationale.Length is < 1 or > 2000)
            return "A review records a rationale of at most 2000 characters.";
        if (content.Exceptions!.Count > 20 || content.Exceptions.Any(static item => item.Length is < 1 or > 1000) ||
            content.Conclusion == "acceptable" && content.Exceptions.Count > 0 ||
            content.Conclusion == "acceptable_with_exceptions" && content.Exceptions.Count == 0)
            return "Review exceptions are required with acceptable_with_exceptions, forbidden with acceptable and bounded to 20.";
        if (content.AssuranceReportId == Uuid.Empty || content.AssuranceReportId is null && content.Evidence is null)
            return "A review requires evidence: a recorded assurance report or a classified evidence citation.";
        return CitationError(content.Evidence);
    }

    /// <summary>Unclassified or non-public metadata and the content it describes are least-privilege by default.</summary>
    public static bool IsRestricted(ProviderSourceCitation? citation) =>
        citation?.MetadataClassification is not ("public" or "internal");

    public static AssuranceReportView Redact(AssuranceReportView view) => view with
    {
        Content = view.Content with
        {
            Exceptions = [],
            ComplementaryControls = [],
            CoverageGaps = [],
            CoveredServices = [],
            Citation = null,
        },
        Redacted = true,
    };

    public static ProviderReviewView Redact(ProviderReviewView view) => view.Content.Evidence is { } evidence &&
        evidence.MetadataClassification is "confidential" or "restricted"
        ? view with { Content = view.Content with { Evidence = null }, Redacted = true }
        : view;

    static string? CitationError(ProviderSourceCitation? citation) => citation is null ? null :
        !Classifications.Contains(citation.MetadataClassification, StringComparer.Ordinal) ||
        citation.ArtifactKind.Length is < 1 or > 200 || citation.Title.Length is < 1 or > 200 ||
        citation.VersionOrDate.Length is < 1 or > 200 || citation.Locator.Length is < 1 or > 1000 ||
        citation.ArtifactId == Uuid.Empty
            ? "A citation requires bounded metadata explicitly classified public, internal, confidential or restricted."
            : null;

    static ProviderSourceCitation? Normalize(ProviderSourceCitation? citation) => citation is null ? null : citation with
    {
        ArtifactKind = citation.ArtifactKind?.Trim() ?? string.Empty,
        Title = citation.Title?.Trim() ?? string.Empty,
        VersionOrDate = citation.VersionOrDate?.Trim() ?? string.Empty,
        Locator = citation.Locator?.Trim() ?? string.Empty,
        MetadataClassification = citation.MetadataClassification?.Trim() ?? string.Empty,
    };

    static bool InRange(DateOnly date) => date >= EarliestDate && date < LatestDate;

    static System.Collections.ObjectModel.ReadOnlyCollection<string> List(IReadOnlyList<string>? items) =>
        Array.AsReadOnly((items ?? []).Select(static item => item?.Trim() ?? string.Empty).ToArray());

    static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

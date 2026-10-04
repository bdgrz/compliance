using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>Validation and canonicalization for provider-specific coverage gaps.</summary>
public static class ProviderCoverageGapRules
{
    public const int MaximumTextLength = 2_000;

    static readonly HashSet<string> SourceKinds = new(StringComparer.Ordinal)
    {
        "assurance_report",
        "provider_review",
        "service_commitment",
        "system_requirement",
        "subservice_responsibility",
        "evidence_artifact",
    };

    public static string? InputError(ProviderCoverageGapContent? content)
    {
        if (content is null)
            return "A provider coverage gap requires exact source facts.";
        if (content.ServiceId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(content.Service) || content.Service.Length > MaximumTextLength ||
            string.IsNullOrWhiteSpace(content.Assertion) || content.Assertion.Length > MaximumTextLength ||
            string.IsNullOrWhiteSpace(content.Description) || content.Description.Length > MaximumTextLength)
            return "A provider coverage gap requires bounded service, assertion and description text.";
        if (content.PeriodEnd < content.PeriodStart)
            return "A provider coverage gap period must end on or after it starts.";
        if (content.SourceKind is null || !SourceKinds.Contains(content.SourceKind.Trim()) || content.SourceId == Uuid.Empty ||
            content.SourceRevision < 1)
            return "A provider coverage gap requires a supported, versioned source reference.";
        return null;
    }

    public static ProviderCoverageGapContent Normalize(ProviderCoverageGapContent content) => content with
    {
        Service = content.Service.Trim(),
        Assertion = content.Assertion.Trim(),
        SourceKind = content.SourceKind.Trim(),
        Description = content.Description.Trim(),
    };

    public static string? ClosureInputError(ProviderCoverageGapClosureContent? content)
    {
        if (content is null)
            return "A provider coverage gap closure requires exact resolution evidence.";
        if (content.Resolution != "coverage_restored")
            return "A provider coverage gap closes only when later evidence restores coverage.";
        if (content.SourceKind is null || !SourceKinds.Contains(content.SourceKind.Trim()) || content.SourceId == Uuid.Empty ||
            content.SourceRevision < 1)
            return "A provider coverage gap closure requires a supported, versioned source reference.";
        if (string.IsNullOrWhiteSpace(content.Rationale) || content.Rationale.Length > MaximumTextLength)
            return "A provider coverage gap closure requires a bounded rationale.";
        return null;
    }

    public static ProviderCoverageGapClosureContent Normalize(ProviderCoverageGapClosureContent content) =>
        content with
        {
            Resolution = content.Resolution.Trim(),
            SourceKind = content.SourceKind.Trim(),
            Rationale = content.Rationale.Trim(),
        };

    public static string? RiskAcceptanceInputError(
        ProviderCoverageGapRiskAcceptanceContent? content) =>
        content is null || content.ProgramId == Uuid.Empty || content.RiskId == Uuid.Empty ||
        content.AcceptanceId == Uuid.Empty
            ? "A provider coverage gap risk acceptance link requires program, risk and acceptance identities."
            : null;

    public static ProviderCoverageGapView Redact(ProviderCoverageGapView view) => view with
    {
        Content = view.Content with { Description = "[redacted]" },
        Closure = view.Closure is { } closure
            ? closure with
            {
                Content = closure.Content with { Rationale = "[redacted]" },
            }
            : null,
        RiskAcceptances = [],
        Redacted = true,
    };
}

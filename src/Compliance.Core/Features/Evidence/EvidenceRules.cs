namespace Bdgrz.Compliance.Features.Evidence;

static class EvidenceRules
{
    public const int MaximumTextLength = 4000;
    const int MaximumTitleLength = 200;

    static readonly string[] HandlingClasses = ["public", "internal", "confidential", "restricted"];

    public static EvidenceArtifactContent Normalize(EvidenceArtifactContent content) => content with
    {
        Title = content.Title?.Trim() ?? "",
        Description = string.IsNullOrWhiteSpace(content.Description) ? null : content.Description.Trim(),
        EvidenceType = content.EvidenceType?.Trim() ?? "",
        Source = content.Source?.Trim() ?? "",
        HandlingClass = content.HandlingClass?.Trim() ?? "",
    };

    public static string? Validate(EvidenceArtifactContent content, string sha256, long length) =>
        ValidateMetadata(content) ??
        (length <= 0 || sha256 is not { Length: 64 } || !sha256.All(IsLowerHex)
            ? "Evidence requires a lowercase SHA-256 content digest and a positive length."
            : null);

    public static string? ValidateMetadata(EvidenceArtifactContent content)
    {
        if (content.Title.Length is 0 or > MaximumTitleLength)
            return "Evidence requires a title of at most 200 characters.";
        if (content.EvidenceType.Length is 0 or > MaximumTitleLength ||
            content.Source.Length is 0 or > MaximumTitleLength)
            return "Evidence requires a type and a source of at most 200 characters each.";
        if (content.Description?.Length > MaximumTextLength)
            return "The evidence description is too long.";
        if (!HandlingClasses.Contains(content.HandlingClass, StringComparer.Ordinal))
            return "The handling class must be public, internal, confidential, or restricted.";
        if (content.CapturedAt == default)
            return "Evidence requires its capture time.";
        if (content.PeriodEnd < content.PeriodStart)
            return "The covered period cannot end before it starts.";
        return null;
    }

    static bool IsLowerHex(char c) => c is >= '0' and <= '9' or >= 'a' and <= 'f';
}

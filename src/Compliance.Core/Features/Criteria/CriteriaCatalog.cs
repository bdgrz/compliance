using System.Text.RegularExpressions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria;

/// <summary>Immutable, source-identified platform data; no licensed criterion text is stored.</summary>
public sealed partial class CriteriaCatalog : ICriteriaCatalog
{
    public static CriteriaCatalog Platform { get; } = PlatformCriteriaCatalog.Create();

    readonly IReadOnlyList<Criterion> _entries;
    readonly Dictionary<Uuid, CriteriaCatalogEdition> _byEdition;
    readonly Dictionary<(Uuid EditionId, string Identifier), Criterion> _byId;

    public CriteriaCatalogEdition Edition => Editions[0];
    public IReadOnlyList<CriteriaCatalogEdition> Editions { get; }
    public IReadOnlyList<Criterion> Entries => _entries;

    public CriteriaCatalog(CriteriaCatalogEdition edition, IReadOnlyList<Criterion> entries)
        : this([edition], entries)
    {
    }

    public CriteriaCatalog(IReadOnlyList<CriteriaCatalogEdition> editions,
        IReadOnlyList<Criterion> entries)
    {
        ArgumentNullException.ThrowIfNull(editions);
        ArgumentNullException.ThrowIfNull(entries);
        if (editions.Count == 0 || entries.Count == 0)
            throw new ArgumentException("A criteria edition needs source and coverage metadata.");

        var byEdition = new Dictionary<Uuid, CriteriaCatalogEdition>();
        var labels = new HashSet<string>(StringComparer.Ordinal);
        foreach (var edition in editions)
        {
            if (edition.EditionId == Uuid.Empty || edition.Framework != "tsc" ||
                string.IsNullOrWhiteSpace(edition.EditionLabel) ||
                string.IsNullOrWhiteSpace(edition.CoverageNote) ||
                string.IsNullOrWhiteSpace(edition.SourceUrl) ||
                edition.ContentRights != "identifiers_and_original_summaries" ||
                edition.SupportGaps is null ||
                !byEdition.TryAdd(edition.EditionId, edition) ||
                !labels.Add(edition.EditionLabel))
                throw new ArgumentException("The criteria catalog has invalid or duplicate editions.");
        }

        var byId = new Dictionary<(Uuid, string), Criterion>();
        foreach (var entry in entries)
        {
            if (!byEdition.ContainsKey(entry.EditionId) ||
                string.IsNullOrWhiteSpace(entry.Identifier) ||
                string.IsNullOrWhiteSpace(entry.Summary) ||
                entry.Category is not ("security" or "availability" or "confidentiality" or
                    "processing_integrity" or "privacy") ||
                entry.Kind is not ("criterion" or "point_of_focus") ||
                !byId.TryAdd((entry.EditionId, entry.Identifier), entry))
                throw new ArgumentException("The criteria edition contains an invalid or duplicate entry.");
        }
        foreach (var entry in entries)
        {
            if (entry.Kind == "criterion")
            {
                if (entry.ParentIdentifier is not null || entry.SourceIdentifier != entry.Identifier ||
                    !SourceIdentifierPattern(entry.Category).IsMatch(entry.Identifier))
                    throw new ArgumentException("A criterion needs its source identifier and no parent.");
            }
            else if (entry.SourceIdentifier is not null ||
                     !entry.Identifier.StartsWith("bdgrz:", StringComparison.Ordinal) ||
                     entry.ParentIdentifier is not { } parentId ||
                     !byId.TryGetValue((entry.EditionId, parentId), out var parent) ||
                     parent.Kind != "criterion" || parent.Category != entry.Category)
                throw new ArgumentException("A point of focus needs a local ID and a criterion parent.");
        }

        Editions = Array.AsReadOnly(editions.OrderBy(static edition => edition.EditionLabel,
            StringComparer.Ordinal).ToArray());
        _entries = Array.AsReadOnly(entries.OrderBy(static entry => entry.EditionId.ToString(),
            StringComparer.Ordinal).ThenBy(static entry => entry.Identifier,
            StringComparer.Ordinal).ToArray());
        _byEdition = byEdition;
        _byId = byId;
    }

    public CriteriaCatalogEdition? GetEdition(Uuid editionId) =>
        _byEdition.GetValueOrDefault(editionId);

    public Criterion? GetEntry(Uuid editionId, string identifier) =>
        identifier is not null && _byId.TryGetValue((editionId, identifier), out var entry)
            ? entry : null;

    public IReadOnlyList<Criterion> ListEntries(Uuid editionId, string? category, string? kind,
        string? parentIdentifier) => !_byEdition.ContainsKey(editionId)
            ? []
            : _entries.Where(entry =>
                entry.EditionId == editionId &&
                (category is null || entry.Category == category) &&
                (kind is null || entry.Kind == kind) &&
                (parentIdentifier is null || entry.ParentIdentifier == parentIdentifier)).ToArray();

    static Regex SourceIdentifierPattern(string category) => category switch
    {
        "security" => SecurityIdentifier(),
        "availability" => AvailabilityIdentifier(),
        "confidentiality" => ConfidentialityIdentifier(),
        "processing_integrity" => ProcessingIntegrityIdentifier(),
        _ => PrivacyIdentifier(),
    };

    [GeneratedRegex(@"\ACC[1-9]\.[1-9][0-9]?\z", RegexOptions.CultureInvariant)]
    private static partial Regex SecurityIdentifier();

    [GeneratedRegex(@"\AA1\.[1-9][0-9]?\z", RegexOptions.CultureInvariant)]
    private static partial Regex AvailabilityIdentifier();

    [GeneratedRegex(@"\AC1\.[1-9][0-9]?\z", RegexOptions.CultureInvariant)]
    private static partial Regex ConfidentialityIdentifier();

    [GeneratedRegex(@"\API1\.[1-9][0-9]?\z", RegexOptions.CultureInvariant)]
    private static partial Regex ProcessingIntegrityIdentifier();

    [GeneratedRegex(@"\AP[1-8]\.[1-9][0-9]?\z", RegexOptions.CultureInvariant)]
    private static partial Regex PrivacyIdentifier();
}

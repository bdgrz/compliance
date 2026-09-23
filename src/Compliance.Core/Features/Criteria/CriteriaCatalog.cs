using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria;

public interface ICriteriaCatalog
{
    IReadOnlyList<CriteriaCatalogEdition> Editions { get; }
    CriteriaCatalogEdition? GetEdition(Uuid editionId);
    Criterion? GetEntry(Uuid editionId, string identifier);
    IReadOnlyList<Criterion> ListEntries(Uuid editionId, string? category, string? kind,
        string? parentIdentifier);
}

/// <summary>Immutable, source-identified platform data; no licensed criterion text is stored.</summary>
public sealed class CriteriaCatalog : ICriteriaCatalog
{
    static readonly Uuid FoundationId = Uuid.Parse("7caef9a8-c521-5bc3-9988-f4b2d49f143b",
        CultureInfo.InvariantCulture);
    const string SourceUrl =
        "https://www.aicpa-cima.com/resources/download/2017-trust-services-criteria-with-revised-points-of-focus-2022";

    public static CriteriaCatalog Foundation { get; } = new(
        new CriteriaCatalogEdition(FoundationId, "tsc", "bdgrz_partial_2017_tsc_2022_pof",
            new DateTimeOffset(2026, 9, 23, 0, 0, 0, TimeSpan.Zero), false,
            "Partial authored foundation: five source criteria and one locally identified point of focus. All categories remain incomplete.",
            SourceUrl),
        [
            new(FoundationId, "CC6.1", "CC6.1", "security", "criterion", null,
                "Protect system information and technology with controlled logical access."),
            new(FoundationId, "A1.1", "A1.1", "availability", "criterion", null,
                "Track processing capacity against availability commitments."),
            new(FoundationId, "C1.1", "C1.1", "confidentiality", "criterion", null,
                "Identify confidential information and apply its handling rules."),
            new(FoundationId, "PI1.1", "PI1.1", "processing_integrity", "criterion", null,
                "Define information quality needs for reliable processing."),
            new(FoundationId, "P1.1", "P1.1", "privacy", "criterion", null,
                "Explain privacy practices to the people whose information is processed."),
            new(FoundationId, "bdgrz:focus:cc6-1:asset-inventory", null, "security",
                "point_of_focus", "CC6.1",
                "Maintain a classified inventory of information and technology assets."),
        ]);

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
                if (entry.ParentIdentifier is not null || entry.SourceIdentifier != entry.Identifier)
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
}

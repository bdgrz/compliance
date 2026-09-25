using Bdgrz.Compliance.Features.Criteria;

namespace Bdgrz.Compliance.Tests.Features.Criteria;

public sealed class PlatformCriteriaCatalogTests
{
    static readonly string[] SourceCriteria =
    [
        "CC1.1", "CC1.2", "CC1.3", "CC1.4", "CC1.5",
        "CC2.1", "CC2.2", "CC2.3",
        "CC3.1", "CC3.2", "CC3.3", "CC3.4",
        "CC4.1", "CC4.2",
        "CC5.1", "CC5.2", "CC5.3",
        "CC6.1", "CC6.2", "CC6.3", "CC6.4", "CC6.5", "CC6.6", "CC6.7", "CC6.8",
        "CC7.1", "CC7.2", "CC7.3", "CC7.4", "CC7.5",
        "CC8.1",
        "CC9.1", "CC9.2",
        "A1.1", "A1.2", "A1.3",
        "C1.1", "C1.2",
        "PI1.1", "PI1.2", "PI1.3", "PI1.4", "PI1.5",
        "P1.1", "P2.1", "P3.1", "P3.2", "P4.1", "P4.2", "P4.3", "P5.1", "P5.2",
        "P6.1", "P6.2", "P6.3", "P6.4", "P6.5", "P6.6", "P6.7", "P7.1", "P8.1",
    ];


    [Fact]
    public void ShouldSeedEveryNumberedCriterionGivenPlatformEdition()
    {
        // Arrange
        var catalog = CriteriaCatalog.Platform;

        // Act
        var edition = Assert.Single(catalog.Editions);
        var criteria = catalog.ListEntries(edition.EditionId, null, "criterion", null);

        // Assert
        Assert.Equal("tsc", edition.Framework);
        Assert.Equal("2017_tsc_2022_pof", edition.EditionLabel);
        Assert.True(edition.IsComplete);
        Assert.Equal("identifiers_and_original_summaries", edition.ContentRights);
        Assert.Equal(SourceCriteria.Order(StringComparer.Ordinal),
            criteria.Select(static entry => entry.Identifier).Order(StringComparer.Ordinal));
        Assert.All(criteria, entry =>
        {
            Assert.Equal(entry.Identifier, entry.SourceIdentifier);
            Assert.Null(entry.ParentIdentifier);
        });
        Assert.Equal(33, criteria.Count(static entry => entry.Category == "security"));
        Assert.Equal(3, criteria.Count(static entry => entry.Category == "availability"));
        Assert.Equal(2, criteria.Count(static entry => entry.Category == "confidentiality"));
        Assert.Equal(5, criteria.Count(static entry => entry.Category == "processing_integrity"));
        Assert.Equal(18, criteria.Count(static entry => entry.Category == "privacy"));
    }

    [Fact]
    public void ShouldSeedMappableLocalFocusPointsGivenEveryCategory()
    {
        // Arrange
        var catalog = CriteriaCatalog.Platform;
        var edition = catalog.Editions[0];

        // Act
        var focus = catalog.ListEntries(edition.EditionId, null, "point_of_focus", null);

        // Assert
        foreach (var category in new[]
                 {
                     "security", "availability", "confidentiality", "processing_integrity", "privacy",
                 })
            Assert.Contains(focus, entry => entry.Category == category);
        Assert.All(focus, entry =>
        {
            Assert.Null(entry.SourceIdentifier);
            Assert.StartsWith("bdgrz:focus:", entry.Identifier, StringComparison.Ordinal);
            var parent = catalog.GetEntry(edition.EditionId, entry.ParentIdentifier!);
            Assert.Equal("criterion", parent?.Kind);
            Assert.Equal(entry.Category, parent?.Category);
        });
    }

    [Fact]
    public void ShouldDeclareSupportGapsGivenOptionalCategoriesAndPartialFocus()
    {
        // Arrange
        var edition = CriteriaCatalog.Platform.Editions[0];

        // Act
        var gaps = edition.SupportGaps;

        // Assert
        Assert.Contains(gaps, gap => gap.Category == "privacy" &&
            gap.Code == "privacy_lifecycle_unsupported");
        foreach (var category in new[]
                 {
                     "security", "availability", "confidentiality", "processing_integrity", "privacy",
                 })
            Assert.Contains(gaps, gap => gap.Category == category &&
                gap.Code == "points_of_focus_partial");
        Assert.All(gaps, gap => Assert.False(string.IsNullOrWhiteSpace(gap.Note)));
    }

    [Fact]
    public void ShouldKeepSummariesShortAndOutcomeFocusedGivenPlatformSeed()
    {
        // Arrange
        var catalog = CriteriaCatalog.Platform;

        // Act
        var summaries = catalog.Entries.Select(static entry => entry.Summary).ToArray();

        // Assert
        Assert.All(summaries, summary =>
        {
            Assert.InRange(summary.Length, 20, 140);
            Assert.False(summary.StartsWith("The entity", StringComparison.OrdinalIgnoreCase),
                summary);
        });
        Assert.Equal(summaries.Length, summaries.Distinct(StringComparer.Ordinal).Count());
    }

    // Optional originality gate. The reference publication is licensed and never enters this
    // repository; a reviewer may point this variable at a locally held plain-text copy.
    [Fact]
    public void ShouldShareNoFiveWordRunGivenOutOfRepoReferenceFile()
    {
        // Arrange
        var path = Environment.GetEnvironmentVariable("BDGRZ_TSC_REFERENCE_FILE");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;
        var referenceRuns = Runs(File.ReadAllText(path), 5).ToHashSet(StringComparer.Ordinal);

        // Act
        var summaries = CriteriaCatalog.Platform.Entries.Select(static entry => entry.Summary);

        // Assert
        Assert.All(summaries, summary =>
            Assert.DoesNotContain(Runs(summary, 5), run => referenceRuns.Contains(run)));
    }

    [Theory]
    [InlineData("CC6.1", "availability")]
    [InlineData("CC10.1", "security")]
    [InlineData("cc6.1", "security")]
    [InlineData("PI1.1", "privacy")]
    [InlineData("X1.1", "security")]
    public void ShouldRejectCriterionGivenIdentifierOutsideItsCategory(string identifier,
        string category)
    {
        // Arrange
        var edition = CriteriaCatalog.Platform.Editions[0];
        var entry = new Criterion(edition.EditionId, identifier, identifier, category,
            "criterion", null, "Summarize a malformed test identifier.");

        // Act
        // Assert
        Assert.Throws<ArgumentException>(() => new CriteriaCatalog(edition, [entry]));
    }

    static IEnumerable<string> Runs(string text, int width)
    {
        var words = text.ToLowerInvariant()
            .Split([' ', ',', '.', ';', '(', ')', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index + width <= words.Length; index++)
            yield return string.Join(' ', words, index, width);
    }
}

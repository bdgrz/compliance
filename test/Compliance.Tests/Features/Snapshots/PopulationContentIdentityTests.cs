using System.Text.Json;
using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Snapshots;

public sealed class PopulationContentIdentityTests
{
    [Fact]
    public void ShouldProduceStableDigestGivenEquivalentPopulationRows()
    {
        // Arrange
        PopulationRow[] original =
        [
            Row("account:alice", """{"name":"Café","active":true,"roles":["admin","dev"]}"""),
            Row("account:bob", """{"active":false,"name":"Bob","roles":[]}""")
        ];
        PopulationRow[] reordered =
        [
            Row("account:alice", """{"roles":["admin","dev"],"active":true,"name":"Café"}"""),
            Row("account:bob", """{"roles":[],"name":"Bob","active":false}""")
        ];
        PopulationRow[] changed =
        [
            Row("account:alice", """{"name":"Café","active":true,"roles":["dev","admin"]}"""),
            Row("account:bob", """{"active":false,"name":"Bob","roles":[]}""")
        ];

        // Act
        var first = PopulationContentIdentity.Compute("access_population", original);
        var equivalent = PopulationContentIdentity.Compute("access_population", reordered);
        var reorderedArray = PopulationContentIdentity.Compute("access_population", changed);
        var otherKind = PopulationContentIdentity.Compute("workforce_roster", original);

        // Assert
        var digest = Assert.IsType<PopulationDigest>(first.Value);
        Assert.Equal(2, digest.RowCount);
        Assert.Equal(1, digest.ChunkCount);
        Assert.Matches("^[0-9a-f]{64}$", digest.Sha256);
        Assert.Equal(digest, equivalent.Value);
        Assert.NotEqual(digest.Sha256, reorderedArray.Value!.Sha256);
        Assert.NotEqual(digest.Sha256, otherKind.Value!.Sha256);
    }

    [Fact]
    public void ShouldRejectPopulationGivenUnorderedDuplicateOrUnsupportedRows()
    {
        // Arrange
        PopulationRow[] unordered = [Row("b", "{}"), Row("a", "{}")];
        PopulationRow[] duplicate = [Row("a", "{}"), Row("a", "{}")];
        PopulationRow[] blankKey = [Row(" ", "{}")];
        PopulationRow[] fraction = [Row("a", """{"score":1.5}""")];

        // Act
        var results = new[]
        {
            PopulationContentIdentity.Compute("access_population", unordered),
            PopulationContentIdentity.Compute("access_population", duplicate),
            PopulationContentIdentity.Compute("access_population", blankKey),
            PopulationContentIdentity.Compute("access_population", fraction),
            PopulationContentIdentity.Compute(" ", [])
        };

        // Assert
        Assert.All(results, result => Assert.Equal(RequestErrorKind.Validation,
            Assert.IsType<RequestError>(result.Error).Kind));
    }

    [Fact]
    public void ShouldTreatPropertyNamesCanonicallyGivenUnicodeForms()
    {
        // Arrange
        PopulationRow[] composed = [Row("a", """{"Café":1}""")];
        PopulationRow[] decomposed = [Row("a", """{"Café":1}""")];
        PopulationRow[] collision = [Row("a", """{"Café":1,"Café":2}""")];

        // Act
        var first = PopulationContentIdentity.Compute("access_population", composed);
        var second = PopulationContentIdentity.Compute("access_population", decomposed);
        var colliding = PopulationContentIdentity.Compute("access_population", collision);

        // Assert
        Assert.Equal(first.Value, second.Value);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(colliding.Error).Kind);
    }

    [Fact]
    public void ShouldFailClosedGivenLoneSurrogatesOrInvalidKind()
    {
        // Arrange
        PopulationRow[] loneKey = [Row("user\uD800", "{}")];
        PopulationRow[] loneContent = [Row("a", """{"name":"bad\uD800"}""")];
        PopulationRow[] loneName = [Row("a", """{"bad\uD800":1}""")];

        // Act
        var results = new[]
        {
            PopulationContentIdentity.Compute("access_population", loneKey),
            PopulationContentIdentity.Compute("access_population", loneContent),
            PopulationContentIdentity.Compute("access_population", loneName),
            PopulationContentIdentity.Compute("Access Population", []),
            PopulationContentIdentity.Compute("access_populatioń", [])
        };

        // Assert
        Assert.All(results, result => Assert.Equal(RequestErrorKind.Validation,
            Assert.IsType<RequestError>(result.Error).Kind));
    }

    [Fact]
    public void ShouldOrderKeysByCodePointGivenSupplementaryCharacters()
    {
        // Arrange
        PopulationRow[] codePointOrder = [Row("！", "{}"), Row("\U0001F600", "{}")];
        PopulationRow[] utf16Order = [Row("\U0001F600", "{}"), Row("！", "{}")];

        // Act
        var accepted = PopulationContentIdentity.Compute("access_population", codePointOrder);
        var rejected = PopulationContentIdentity.Compute("access_population", utf16Order);

        // Assert
        Assert.Equal(2, Assert.IsType<PopulationDigest>(accepted.Value).RowCount);
        var error = Assert.IsType<RequestError>(rejected.Error);
        Assert.Equal(RequestErrorKind.Validation, error.Kind);
        Assert.Contains("row 2", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldNotMaskDisposedContentGivenProgrammingError()
    {
        // Arrange
        JsonElement disposed;
        using (var document = JsonDocument.Parse("{}"))
            disposed = document.RootElement;
        PopulationRow[] rows = [new PopulationRow("a", disposed)];

        // Act
        Action compute = () => PopulationContentIdentity.Compute("access_population", rows);

        // Assert
        Assert.Throws<ObjectDisposedException>(compute);
    }

    [Fact]
    public void ShouldDigestEmptyPopulationGivenExplicitZeroRows()
    {
        // Arrange
        PopulationRow[] rows = [];

        // Act
        var empty = PopulationContentIdentity.Compute("access_population", rows);

        // Assert
        var digest = Assert.IsType<PopulationDigest>(empty.Value);
        Assert.Equal(0, digest.RowCount);
        Assert.Equal(0, digest.ChunkCount);
        Assert.Matches("^[0-9a-f]{64}$", digest.Sha256);
    }

    [Fact]
    public void ShouldDigestSupportedBoundGivenStreamedPopulation()
    {
        // Arrange
        var maximum = PopulationContentIdentity.MaximumRows;

        // Act
        var first = PopulationContentIdentity.Compute("access_population", Rows(maximum));
        var replay = PopulationContentIdentity.Compute("access_population", Rows(maximum));

        // Assert
        var digest = Assert.IsType<PopulationDigest>(first.Value);
        Assert.Equal(maximum, digest.RowCount);
        Assert.Equal((int)((maximum + PopulationContentIdentity.RowsPerChunk - 1) /
                           PopulationContentIdentity.RowsPerChunk), digest.ChunkCount);
        Assert.Equal(digest, replay.Value);
    }

    [Fact]
    public void ShouldStopReadingGivenPopulationBeyondSupportedBound()
    {
        // Arrange
        var maximum = PopulationContentIdentity.MaximumRows;
        var read = 0L;

        // Act
        var result = PopulationContentIdentity.Compute("access_population",
            Rows(maximum + 10, () => read++));

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Equal(maximum + 1, read);
    }

    static PopulationRow Row(string key, string json)
    {
        using var document = JsonDocument.Parse(json);
        return new PopulationRow(key, document.RootElement.Clone());
    }

    static IEnumerable<PopulationRow> Rows(long count, Action? onRead = null)
    {
        using var document = JsonDocument.Parse("""{"active":true,"kind":"account"}""");
        var content = document.RootElement.Clone();
        for (var index = 0L; index < count; index++)
        {
            onRead?.Invoke();
            yield return new PopulationRow($"principal:{index:D9}", content);
        }
    }
}

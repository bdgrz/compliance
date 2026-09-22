using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Snapshots;

static class PopulationContentIdentity
{
    internal const int RowsPerChunk = 1024;
    internal const long MaximumRows = 250_000;

    static readonly byte[] RowDomain = Encoding.UTF8.GetBytes("bdgrz.snapshot.population.row.v1\0");
    static readonly byte[] ChunkDomain = Encoding.UTF8.GetBytes("bdgrz.snapshot.population.chunk.v1\0");
    static readonly byte[] PopulationDomain = Encoding.UTF8.GetBytes("bdgrz.snapshot.population.v1\0");

    public static Result<PopulationDigest> Compute(string kind, IEnumerable<PopulationRow> rows)
    {
        if (string.IsNullOrWhiteSpace(kind) || kind != kind.Trim())
            return Invalid("A population snapshot requires a kind.");

        using var population = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var chunk = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var content = new MemoryStream();
        var digest = new byte[SHA256.HashSizeInBytes];
        string? previousKey = null;
        var rowCount = 0L;
        var chunkCount = 0;
        var rowsInChunk = 0;

        foreach (var row in rows)
        {
            if (rowCount == MaximumRows)
                return Invalid($"A population snapshot supports at most {MaximumRows} rows.");
            if (string.IsNullOrWhiteSpace(row.Key) || row.Key != row.Key.Trim())
                return Invalid("Every population row requires a stable key.");
            var key = row.Key.Normalize(NormalizationForm.FormC);
            if (previousKey is not null && string.CompareOrdinal(previousKey, key) >= 0)
                return Invalid("Population rows must be unique and ordered by stable key.");
            if (!TryWriteCanonical(content, row.Content))
                return Invalid("Population row content must use canonical v1 JSON values.");

            if (rowsInChunk == 0)
                chunk.AppendData(ChunkDomain);
            WriteRowDigest(key, content, digest);
            chunk.AppendData(digest);
            previousKey = key;
            rowCount++;
            if (++rowsInChunk == RowsPerChunk)
            {
                AppendChunk(population, chunk, digest);
                chunkCount++;
                rowsInChunk = 0;
            }
        }
        if (rowsInChunk > 0)
        {
            AppendChunk(population, chunk, digest);
            chunkCount++;
        }

        var header = new byte[12];
        BinaryPrimitives.WriteInt64BigEndian(header, rowCount);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(8), chunkCount);
        using var root = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        root.AppendData(PopulationDomain);
        root.AppendData(Encoding.UTF8.GetBytes(kind));
        root.AppendData([0]);
        root.AppendData(header);
        root.AppendData(population.GetHashAndReset());
        return Result<PopulationDigest>.Success(new PopulationDigest(rowCount, chunkCount,
            Convert.ToHexStringLower(root.GetHashAndReset())));
    }

    static bool TryWriteCanonical(MemoryStream destination, JsonElement content)
    {
        destination.SetLength(0);
        try
        {
            using var writer = new Utf8JsonWriter(destination);
            SnapshotContentIdentity.WriteCanonical(writer, content);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    static void WriteRowDigest(string key, MemoryStream content, byte[] destination)
    {
        using var row = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        row.AppendData(RowDomain);
        row.AppendData(Encoding.UTF8.GetBytes(key));
        row.AppendData([0]);
        row.AppendData(content.GetBuffer(), 0, (int)content.Length);
        row.GetHashAndReset(destination);
    }

    static void AppendChunk(IncrementalHash population, IncrementalHash chunk, byte[] digest)
    {
        chunk.GetHashAndReset(digest);
        population.AppendData(digest);
    }

    static Result<PopulationDigest> Invalid(string message) =>
        Result<PopulationDigest>.Failure(new RequestError(RequestErrorKind.Validation, message));
}

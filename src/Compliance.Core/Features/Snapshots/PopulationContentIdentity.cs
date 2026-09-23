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

    static readonly UTF8Encoding StrictUtf8 = new(false, true);
    static readonly byte[] RowDomain = Encoding.UTF8.GetBytes("bdgrz.snapshot.population.row.v1\0");
    static readonly byte[] ChunkDomain = Encoding.UTF8.GetBytes("bdgrz.snapshot.population.chunk.v1\0");
    static readonly byte[] PopulationDomain = Encoding.UTF8.GetBytes("bdgrz.snapshot.population.v1\0");

    public static Result<PopulationDigest> Compute(string kind, IEnumerable<PopulationRow> rows)
    {
        if (!IsKind(kind))
            return Invalid("A population snapshot requires a lowercase snake_case kind.");

        using var population = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var chunk = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var row = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var content = new MemoryStream();
        using var writer = new Utf8JsonWriter(content, SnapshotContentIdentity.CanonicalWriterOptions);
        var digest = new byte[SHA256.HashSizeInBytes];
        byte[]? previousKey = null;
        var rowCount = 0L;
        var chunkCount = 0;
        var rowsInChunk = 0;

        foreach (var source in rows)
        {
            var position = rowCount + 1;
            if (rowCount == MaximumRows)
                return Invalid($"A population snapshot supports at most {MaximumRows} rows; row {position} exceeds it.");
            if (!TryEncodeKey(source.Key, out var key))
                return Invalid($"Population row {position} requires a stable, valid Unicode key.");
            if (previousKey is not null && previousKey.AsSpan().SequenceCompareTo(key) >= 0)
                return Invalid($"Population row {position} is not unique and ordered by stable key.");
            if (!TryWriteCanonical(writer, content, source.Content))
                return Invalid($"Population row {position} content must use canonical v1 JSON values.");

            row.AppendData(RowDomain);
            row.AppendData(key);
            row.AppendData([0]);
            row.AppendData(content.GetBuffer(), 0, (int)content.Length);
            row.GetHashAndReset(digest);
            if (rowsInChunk == 0)
                chunk.AppendData(ChunkDomain);
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

    static bool IsKind(string kind)
    {
        if (string.IsNullOrEmpty(kind) || kind[0] is < 'a' or > 'z')
            return false;
        foreach (var character in kind)
        {
            if (character is not ((>= 'a' and <= 'z') or (>= '0' and <= '9') or '_'))
                return false;
        }
        return true;
    }

    // Keys compare as UTF-8 bytes, which is Unicode code-point order.
    static bool TryEncodeKey(string key, out byte[] encoded)
    {
        encoded = [];
        if (string.IsNullOrWhiteSpace(key) || key != key.Trim())
            return false;
        try
        {
            encoded = StrictUtf8.GetBytes(SnapshotContentIdentity.NormalizeText(key));
            return true;
        }
        catch (Exception exception) when (exception is SnapshotContentException or EncoderFallbackException)
        {
            return false;
        }
    }

    static bool TryWriteCanonical(Utf8JsonWriter writer, MemoryStream destination, JsonElement content)
    {
        destination.SetLength(0);
        writer.Reset(destination);
        try
        {
            SnapshotContentIdentity.WriteCanonical(writer, content);
            writer.Flush();
            return true;
        }
        catch (SnapshotContentException)
        {
            return false;
        }
    }

    static void AppendChunk(IncrementalHash population, IncrementalHash chunk, byte[] digest)
    {
        chunk.GetHashAndReset(digest);
        population.AppendData(digest);
    }

    static Result<PopulationDigest> Invalid(string message) =>
        Result<PopulationDigest>.Failure(new RequestError(RequestErrorKind.Validation, message));
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Programs;

namespace Bdgrz.Compliance.Features.Snapshots;

static class SnapshotContentIdentity
{
    public static string ProgramRevision(ProgramRevisionView revision) =>
        Digest("bdgrz.snapshot.source.program_revision.v1\0",
            CanonicalSource(revision, ComplianceCoreJsonContext.Default.ProgramRevisionView));

    public static string ApprovedBoundaryVersion(BoundaryVersionView version) =>
        Digest("bdgrz.snapshot.source.approved_boundary_version.v1\0",
            CanonicalSource(version, ComplianceCoreJsonContext.Default.BoundaryVersionView));

    public static (string Json, string Digest) Manifest(ProgramScopeManifest manifest)
    {
        if (manifest.FormatVersion != 1)
            throw new ArgumentOutOfRangeException(nameof(manifest),
                "Only program scope manifest format v1 is supported.");
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, CanonicalWriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber("format_version", manifest.FormatVersion);
            writer.WriteString("kind", "program_scope");
            writer.WriteString("tenant_id", manifest.TenantId.ToString());
            writer.WriteString("program_id", manifest.ProgramId.ToString());
            writer.WriteNumber("program_revision", manifest.ProgramRevision);
            writer.WriteString("program_content_sha256", manifest.ProgramContentSha256);
            writer.WriteString("boundary_id", manifest.BoundaryId.ToString());
            writer.WriteString("approved_boundary_version_id",
                manifest.ApprovedBoundaryVersionId.ToString());
            writer.WriteString("boundary_content_sha256", manifest.BoundaryContentSha256);
            writer.WriteEndObject();
        }
        var bytes = stream.ToArray();
        return (Encoding.UTF8.GetString(bytes),
            Digest("bdgrz.snapshot.manifest.program_scope.v1\0", bytes));
    }

    public static bool MatchesManifest(ProgramScopeManifest manifest, string json, string digest)
    {
        if (manifest.FormatVersion != 1)
            return false;
        var expected = Manifest(manifest);
        return string.Equals(json, expected.Json, StringComparison.Ordinal) &&
               string.Equals(digest, expected.Digest, StringComparison.Ordinal);
    }

    public static bool MatchesView(SnapshotView view) =>
        view.Kind == "program_scope" && view.Revision == 1 &&
        view.Manifest.TenantId == view.TenantId &&
        view.Manifest.ProgramId == view.ProgramId &&
        (view.AmendsSnapshotId is null
            ? view.RootSnapshotId == view.SnapshotId
            : view.RootSnapshotId != view.SnapshotId &&
              view.AmendsSnapshotId != view.SnapshotId) &&
        MatchesManifest(view.Manifest, view.CanonicalManifest, view.ContentSha256);

    static byte[] CanonicalSource<T>(T source, JsonTypeInfo<T> typeInfo)
    {
        var element = JsonSerializer.SerializeToElement(source, typeInfo);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, CanonicalWriterOptions))
            WriteCanonical(writer, element);
        return stream.ToArray();
    }

    // Canonical v1 pins the System.Text.Json default escaping: non-ASCII and
    // HTML-sensitive characters are written as uppercase \uXXXX escapes.
    internal static readonly JsonWriterOptions CanonicalWriterOptions =
        new() { Encoder = JavaScriptEncoder.Default };

    internal static string NormalizeText(string value)
    {
        try
        {
            return value.Normalize(NormalizationForm.FormC);
        }
        catch (ArgumentException exception)
        {
            throw new SnapshotContentException("Snapshot text must be valid Unicode.", exception);
        }
    }

    static string ReadText(Func<string> read)
    {
        try
        {
            return read();
        }
        catch (InvalidOperationException exception) when (exception is not ObjectDisposedException)
        {
            throw new SnapshotContentException("Snapshot text must be valid Unicode.", exception);
        }
    }

    internal static int CompareCodePoints(string left, string right)
    {
        var leftRunes = left.EnumerateRunes();
        var rightRunes = right.EnumerateRunes();
        while (true)
        {
            var hasLeft = leftRunes.MoveNext();
            var hasRight = rightRunes.MoveNext();
            if (!hasLeft || !hasRight)
                return hasLeft.CompareTo(hasRight);
            var comparison = leftRunes.Current.Value.CompareTo(rightRunes.Current.Value);
            if (comparison != 0)
                return comparison;
        }
    }

    internal static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var properties = element.EnumerateObject()
                    .Select(static property => (Name: NormalizeText(ReadText(() => property.Name)),
                        property.Value))
                    .ToList();
                properties.Sort(static (left, right) => CompareCodePoints(left.Name, right.Name));
                writer.WriteStartObject();
                for (var index = 0; index < properties.Count; index++)
                {
                    if (index > 0 && properties[index - 1].Name == properties[index].Name)
                        throw new SnapshotContentException(
                            "Snapshot object property names must be unique after normalization.");
                    writer.WritePropertyName(properties[index].Name);
                    WriteCanonical(writer, properties[index].Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                    WriteCanonical(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(NormalizeText(ReadText(() => element.GetString()!)));
                break;
            case JsonValueKind.Number:
                if (!element.TryGetInt64(out var number))
                    throw new SnapshotContentException(
                        "Snapshot content numbers must be integers within the signed 64-bit range.");
                writer.WriteNumberValue(number);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new SnapshotContentException("An unsupported JSON value entered a snapshot.");
        }
    }

    static string Digest(string domain, ReadOnlySpan<byte> bytes)
    {
        var prefix = Encoding.UTF8.GetBytes(domain);
        var material = new byte[prefix.Length + bytes.Length];
        prefix.CopyTo(material, 0);
        bytes.CopyTo(material.AsSpan(prefix.Length));
        return Convert.ToHexStringLower(SHA256.HashData(material));
    }
}

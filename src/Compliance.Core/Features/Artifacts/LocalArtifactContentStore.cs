using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Artifacts;

/// <summary>
/// Development and spike adapter for <see cref="IArtifactContentStore" />. It mirrors the accepted production layout
/// with one directory per tenant in place of one bucket per tenant:
/// <c>{root}/{tenant_id}/content/sha256/{first_two_hex}/{sha256}</c>. Production content uses the Portia S3 adapter.
/// </summary>
public sealed class LocalArtifactContentStore(ArtifactContentStoreOptions options, TimeProvider time)
    : IArtifactContentStore
{
    const string DeliveryPrefix = "urn:bdgrz:artifact-delivery:";
    const int MinimumDeliveryKeyLength = 32;

    public async ValueTask<ArtifactContentWrite> StoreIfAbsentAsync(Uuid tenantId, Stream content,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        var tenantRoot = TenantRoot(tenantId);
        var staging = Directory.CreateDirectory(Path.Combine(tenantRoot, "staging")).FullName;
        var temporary = Path.Combine(staging, $"{Guid.NewGuid():N}.partial");
        try
        {
            var (digest, length) = await StageAsync(content, temporary, ct).ConfigureAwait(false);
            var reference = new ArtifactContentReference(tenantId, digest, length);
            var target = ContentPath(reference);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (File.Exists(target))
                return await ExistingAsync(reference, ct).ConfigureAwait(false);
            try
            {
                File.Move(temporary, target, overwrite: false);
                return new ArtifactContentWrite(reference, AlreadyPresent: false);
            }
            catch (IOException) when (File.Exists(target))
            {
                return await ExistingAsync(reference, ct).ConfigureAwait(false);
            }
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    public async ValueTask<Stream?> OpenVerifiedAsync(ArtifactContentReference content,
        CancellationToken ct = default)
    {
        var path = ContentPath(content);
        FileStream stream;
        try
        {
            stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete,
                bufferSize: 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }

        try
        {
            if (stream.Length != content.Length)
                throw VerificationFailure();
            var digest = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
            if (!string.Equals(Convert.ToHexStringLower(digest), content.Sha256, StringComparison.Ordinal))
                throw VerificationFailure();
            stream.Position = 0;
            return stream;
        }
        catch
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public ValueTask<ArtifactDelivery?> IssueDeliveryAsync(ArtifactContentReference content, TimeSpan lifetime,
        CancellationToken ct = default)
    {
        if (lifetime <= TimeSpan.Zero || lifetime > ArtifactContentLimits.MaxDeliveryLifetime)
            throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime,
                $"A delivery lifetime must be positive and at most {ArtifactContentLimits.MaxDeliveryLifetime}.");
        var key = DeliveryKey();
        if (!File.Exists(ContentPath(content)))
            return ValueTask.FromResult<ArtifactDelivery?>(null);

        var expiresAt = time.GetUtcNow() + lifetime;
        var payload = Encoding.UTF8.GetBytes(string.Join('\n', content.TenantId.ToString(),
            content.Sha256, content.Length.ToString(CultureInfo.InvariantCulture),
            expiresAt.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)));
        var token = $"{Base64Url.EncodeToString(payload)}.{Base64Url.EncodeToString(HMACSHA256.HashData(key, payload))}";
        return ValueTask.FromResult<ArtifactDelivery?>(
            new ArtifactDelivery(content, new Uri(DeliveryPrefix + token), expiresAt));
    }

    /// <summary>
    /// Opens content for a delivery location this adapter issued. This stands in for the object store serving a
    /// pre-signed URL; it returns null for a forged, malformed, or expired location.
    /// </summary>
    public async ValueTask<Stream?> OpenDeliveryAsync(Uri location, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(location);
        var key = DeliveryKey();
        var value = location.OriginalString;
        if (!value.StartsWith(DeliveryPrefix, StringComparison.Ordinal))
            return null;
        var parts = value[DeliveryPrefix.Length..].Split('.');
        if (parts.Length != 2 || !TryDecode(parts[0], out var payload) || !TryDecode(parts[1], out var signature) ||
            !CryptographicOperations.FixedTimeEquals(HMACSHA256.HashData(key, payload), signature))
            return null;

        var fields = Encoding.UTF8.GetString(payload).Split('\n');
        if (fields.Length != 4 ||
            !Uuid.TryParse(fields[0], CultureInfo.InvariantCulture, out var tenantId) ||
            !long.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out var length) ||
            !long.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out var expires) ||
            time.GetUtcNow().ToUnixTimeMilliseconds() >= expires)
            return null;
        return await OpenVerifiedAsync(new ArtifactContentReference(tenantId, fields[1], length), ct)
            .ConfigureAwait(false);
    }

    public ValueTask<bool> DeleteAsync(ArtifactContentReference content, CancellationToken ct = default)
    {
        var path = ContentPath(content);
        if (!File.Exists(path))
            return ValueTask.FromResult(false);
        File.Delete(path);
        return ValueTask.FromResult(true);
    }

    async ValueTask<(string Digest, long Length)> StageAsync(Stream content, string temporary,
        CancellationToken ct)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            bufferSize: 81920, FileOptions.Asynchronous);
        var buffer = new byte[81920];
        long length = 0;
        int read;
        while ((read = await content.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            length += read;
            if (length > options.MaxContentLength)
                throw new ArgumentOutOfRangeException(nameof(content), length,
                    $"Artifact content may not exceed {options.MaxContentLength} bytes.");
            hash.AppendData(buffer, 0, read);
            await output.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
        }

        output.Flush(flushToDisk: true);
        return (Convert.ToHexStringLower(hash.GetHashAndReset()), length);
    }

    async ValueTask<ArtifactContentWrite> ExistingAsync(ArtifactContentReference reference, CancellationToken ct)
    {
        await using var existing = await OpenVerifiedAsync(reference, ct).ConfigureAwait(false)
            ?? throw VerificationFailure();
        return new ArtifactContentWrite(reference, AlreadyPresent: true);
    }

    string ContentPath(ArtifactContentReference content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Length < 0 || content.Sha256 is not { Length: 64 } digest ||
            !digest.All(char.IsAsciiHexDigitLower))
            throw new ArgumentException("An artifact reference requires a lowercase SHA-256 digest and a length.",
                nameof(content));
        return Path.Combine(TenantRoot(content.TenantId), "content", "sha256", digest[..2], digest);
    }

    string TenantRoot(Uuid tenantId) =>
        Path.Combine(options.LocalRoot is { Length: > 0 } root
            ? root
            : throw new InvalidOperationException(
                "No artifact content store is configured. Set Artifacts:LocalContentRoot."),
            tenantId.ToString());

    byte[] DeliveryKey() =>
        options.DeliveryKey is { Length: >= MinimumDeliveryKeyLength } key
            ? key
            : throw new InvalidOperationException(
                "Artifact delivery requires a key of at least 32 bytes. Set Artifacts:LocalDeliveryKey.");

    static bool TryDecode(string value, out byte[] bytes)
    {
        try
        {
            bytes = Base64Url.DecodeFromChars(value);
            return true;
        }
        catch (FormatException)
        {
            bytes = [];
            return false;
        }
    }

    static InvalidDataException VerificationFailure() =>
        new("Stored artifact content failed digest or length verification.");
}

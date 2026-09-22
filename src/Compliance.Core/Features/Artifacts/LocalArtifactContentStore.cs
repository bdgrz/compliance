using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Artifacts;

/// <summary>
/// Development and spike adapter for <see cref="IArtifactContentStore" />. It mirrors the accepted production layout
/// with one directory per tenant in place of one bucket per tenant: available content at
/// <c>{root}/{tenant_id}/content/sha256/{first_two_hex}/{sha256}</c>, quarantined content under the reserved
/// <c>quarantine/</c> prefix, and one marker per active hold. Production content uses the Portia S3 adapter.
/// </summary>
public sealed class LocalArtifactContentStore(ArtifactContentStoreOptions options, TimeProvider time)
    : IArtifactContentStore
{
    const string DeliveryPrefix = "urn:bdgrz:artifact-delivery:";

    public async ValueTask<ArtifactContentWrite> StoreIfAbsentAsync(Uuid tenantId, Stream content,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        var staging = Directory.CreateDirectory(Path.Combine(TenantRoot(tenantId), "staging")).FullName;
        var temporary = Path.Combine(staging, $"{Guid.NewGuid():N}.partial");
        try
        {
            var (digest, length) = await StageAsync(content, temporary, ct).ConfigureAwait(false);
            var reference = new ArtifactContentReference(tenantId, digest, length);
            var quarantined = QuarantinePath(reference);
            if (File.Exists(quarantined) && await VerifiesAsync(quarantined, reference, ct).ConfigureAwait(false))
                return new ArtifactContentWrite(reference, AlreadyPresent: true);

            var target = AvailablePath(reference);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (File.Exists(target))
                return await ExistingOrRepairAsync(temporary, target, reference, ct).ConfigureAwait(false);
            try
            {
                File.Move(temporary, target, overwrite: false);
                return new ArtifactContentWrite(reference, AlreadyPresent: false);
            }
            catch (IOException) when (File.Exists(target))
            {
                return await ExistingOrRepairAsync(temporary, target, reference, ct).ConfigureAwait(false);
            }
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    public ValueTask<Stream?> OpenVerifiedAsync(ArtifactContentReference content, CancellationToken ct = default) =>
        OpenVerifiedAtAsync(AvailablePath(content), content, ct);

    public ValueTask<ArtifactDelivery?> IssueDeliveryAsync(ArtifactContentReference content, TimeSpan lifetime,
        CancellationToken ct = default)
    {
        if (lifetime <= TimeSpan.Zero || lifetime > ArtifactContentLimits.MaxDeliveryLifetime)
            throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime,
                $"A delivery lifetime must be positive and at most {ArtifactContentLimits.MaxDeliveryLifetime}.");
        var key = DeliveryKey();
        if (!Matches(AvailablePath(content), content))
            return ValueTask.FromResult<ArtifactDelivery?>(null);

        var expires = (time.GetUtcNow() + lifetime).ToUnixTimeMilliseconds();
        var payload = Encoding.UTF8.GetBytes(string.Join('\n', content.TenantId.ToString(),
            content.Sha256, content.Length.ToString(CultureInfo.InvariantCulture),
            expires.ToString(CultureInfo.InvariantCulture)));
        var token = $"{Base64Url.EncodeToString(payload)}.{Base64Url.EncodeToString(HMACSHA256.HashData(key, payload))}";
        return ValueTask.FromResult<ArtifactDelivery?>(new ArtifactDelivery(content,
            new Uri(DeliveryPrefix + token), DateTimeOffset.FromUnixTimeMilliseconds(expires)));
    }

    /// <summary>
    /// Opens content for a delivery location this adapter issued. This stands in for the object store serving a
    /// pre-signed URL; it returns null for a forged, malformed, or expired location, or for withdrawn content.
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

    public ValueTask<bool> PlaceHoldAsync(ArtifactContentReference content, Uuid holdId,
        CancellationToken ct = default)
    {
        if (!Matches(AvailablePath(content), content) && !Matches(QuarantinePath(content), content))
            return ValueTask.FromResult(false);
        var holds = Directory.CreateDirectory(HoldDirectory(content)).FullName;
        File.WriteAllBytes(Path.Combine(holds, holdId.ToString()), []);
        return ValueTask.FromResult(true);
    }

    public ValueTask<bool> RemoveHoldAsync(ArtifactContentReference content, Uuid holdId,
        CancellationToken ct = default)
    {
        var hold = Path.Combine(HoldDirectory(content), holdId.ToString());
        if (!File.Exists(hold))
            return ValueTask.FromResult(false);
        File.Delete(hold);
        return ValueTask.FromResult(true);
    }

    public ValueTask<bool> QuarantineAsync(ArtifactContentReference content, CancellationToken ct = default) =>
        ValueTask.FromResult(MoveIfMatches(AvailablePath(content), QuarantinePath(content), content));

    public ValueTask<bool> ReleaseQuarantineAsync(ArtifactContentReference content,
        CancellationToken ct = default) =>
        ValueTask.FromResult(MoveIfMatches(QuarantinePath(content), AvailablePath(content), content));

    public ValueTask<Stream?> OpenQuarantinedAsync(ArtifactContentReference content,
        CancellationToken ct = default) =>
        OpenVerifiedAtAsync(QuarantinePath(content), content, ct);

    public ValueTask<ArtifactDeletionResult> DeleteAsync(ArtifactContentReference content,
        CancellationToken ct = default)
    {
        var holds = HoldDirectory(content);
        if (Directory.Exists(holds) && Directory.EnumerateFiles(holds).Any())
            return ValueTask.FromResult(ArtifactDeletionResult.Held);
        foreach (var path in (string[])[AvailablePath(content), QuarantinePath(content)])
        {
            if (!Matches(path, content))
                continue;
            File.Delete(path);
            return ValueTask.FromResult(ArtifactDeletionResult.Deleted);
        }

        return ValueTask.FromResult(ArtifactDeletionResult.NotFound);
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

        await output.FlushAsync(ct).ConfigureAwait(false);
        output.Flush(flushToDisk: true);
        return (Convert.ToHexStringLower(hash.GetHashAndReset()), length);
    }

    static async ValueTask<ArtifactContentWrite> ExistingOrRepairAsync(string temporary, string target,
        ArtifactContentReference reference, CancellationToken ct)
    {
        if (await VerifiesAsync(target, reference, ct).ConfigureAwait(false))
            return new ArtifactContentWrite(reference, AlreadyPresent: true);
        File.Move(temporary, target, overwrite: true);
        return new ArtifactContentWrite(reference, AlreadyPresent: false);
    }

    static async ValueTask<bool> VerifiesAsync(string path, ArtifactContentReference reference, CancellationToken ct)
    {
        try
        {
            await using var existing = await OpenVerifiedAtAsync(path, reference, ct).ConfigureAwait(false);
            return existing is not null;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    static async ValueTask<Stream?> OpenVerifiedAtAsync(string path, ArtifactContentReference content,
        CancellationToken ct)
    {
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

    static bool MoveIfMatches(string source, string destination, ArtifactContentReference content)
    {
        if (!Matches(source, content))
            return false;
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        try
        {
            File.Move(source, destination, overwrite: false);
            return true;
        }
        catch (IOException) when (File.Exists(destination))
        {
            File.Delete(source);
            return true;
        }
    }

    static bool Matches(string path, ArtifactContentReference content)
    {
        var file = new FileInfo(path);
        return file.Exists && file.Length == content.Length;
    }

    string AvailablePath(ArtifactContentReference content) => DigestPath(content, "content");

    string QuarantinePath(ArtifactContentReference content) => DigestPath(content, "quarantine");

    string HoldDirectory(ArtifactContentReference content) =>
        Path.Combine(TenantRoot(content.TenantId), "holds", Digest(content));

    string DigestPath(ArtifactContentReference content, string prefix)
    {
        var digest = Digest(content);
        return Path.Combine(TenantRoot(content.TenantId), prefix, "sha256", digest[..2], digest);
    }

    static string Digest(ArtifactContentReference content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return content.Length >= 0 && content.Sha256 is { Length: 64 } digest && digest.All(char.IsAsciiHexDigitLower)
            ? digest
            : throw new ArgumentException(
                "An artifact reference requires a lowercase SHA-256 digest and a length.", nameof(content));
    }

    string TenantRoot(Uuid tenantId) =>
        Path.Combine(options.LocalRoot is { Length: > 0 } root
            ? root
            : throw new InvalidOperationException(
                "No artifact content store is configured. Set Artifacts:LocalContentRoot."),
            tenantId.ToString());

    byte[] DeliveryKey() =>
        options.DeliveryKey is { Length: >= ArtifactContentStoreOptions.MinimumDeliveryKeyLength } key
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

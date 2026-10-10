using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed class SignedFirmProfessionalDutyAssertionVerifier : IFirmProfessionalDutyAssertionVerifier,
    IDisposable
{
    static readonly HashSet<string> HeaderNames = ["alg", "kid", "typ"];
    static readonly HashSet<string> ClaimNames =
    [
        "iss", "aud", "sub", "jti", "designation_id", "duty", "tenant_id", "authority_revision",
        "status", "supersedes_assertion_id", "iat", "nbf", "exp"
    ];

    readonly FirmProfessionalAuthorityTrustSettings? _settings;
    readonly RSA? _publicKey;

    public SignedFirmProfessionalDutyAssertionVerifier(FirmProfessionalAuthorityTrustSettings? settings)
    {
        _settings = settings;
        if (settings is null)
            return;

        _publicKey = RSA.Create();
        _publicKey.ImportSubjectPublicKeyInfo(Convert.FromBase64String(settings.PublicKeyBase64), out _);
    }

    public bool TryVerify(string? signedAssertion, DateTimeOffset now,
        out VerifiedFirmProfessionalDutyAssertion assertion)
    {
        assertion = null!;
        if (_settings is null || _publicKey is null || string.IsNullOrWhiteSpace(signedAssertion) ||
            signedAssertion.Length > 16_384)
            return false;

        var parts = signedAssertion.Split('.');
        if (parts.Length != 3 || !TryDecode(parts[0], out var headerBytes) ||
            !TryDecode(parts[1], out var payloadBytes) || !TryDecode(parts[2], out var signature) ||
            Base64Url(headerBytes) != parts[0] || Base64Url(payloadBytes) != parts[1] ||
            Base64Url(signature) != parts[2])
            return false;

        try
        {
            using var headerDocument = JsonDocument.Parse(headerBytes);
            if (!TryReadObject(headerDocument.RootElement, HeaderNames, out var header) ||
                !StringClaim(header, "alg", out var algorithm) || algorithm != "RS256" ||
                !StringClaim(header, "kid", out var keyId) || keyId != _settings.KeyId ||
                !StringClaim(header, "typ", out var type) || type != "JWT")
                return false;

            var signingInput = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
            if (!_publicKey.VerifyData(signingInput, signature, HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1))
                return false;

            using var payloadDocument = JsonDocument.Parse(payloadBytes);
            if (!TryReadObject(payloadDocument.RootElement, ClaimNames, out var claims) ||
                !StringClaim(claims, "iss", out var issuer) || issuer != _settings.Issuer ||
                !StringClaim(claims, "aud", out var audience) || audience != _settings.Audience ||
                !UuidClaim(claims, "sub", out var userId) ||
                !UuidClaim(claims, "jti", out var assertionId) ||
                !UuidClaim(claims, "designation_id", out var designationId) ||
                !StringClaim(claims, "duty", out var duty) ||
                !SupportedScope(duty, claims, out var tenantId) ||
                !IntegerClaim(claims, "authority_revision", out var authorityRevision) ||
                authorityRevision <= 0 || !StringClaim(claims, "status", out var status) ||
                status is not ("active" or "revoked") ||
                !OptionalUuidClaim(claims, "supersedes_assertion_id", out var supersedesAssertionId) ||
                !IntegerClaim(claims, "iat", out var issuedAtSeconds) ||
                !IntegerClaim(claims, "nbf", out var notBeforeSeconds) ||
                !IntegerClaim(claims, "exp", out var expiresAtSeconds))
                return false;

            if (userId == Uuid.Empty || assertionId == Uuid.Empty || designationId == Uuid.Empty ||
                assertionId == designationId || supersedesAssertionId == assertionId ||
                status == "revoked" && supersedesAssertionId is null)
                return false;

            var issuedAt = DateTimeOffset.FromUnixTimeSeconds(issuedAtSeconds);
            var notBefore = DateTimeOffset.FromUnixTimeSeconds(notBeforeSeconds);
            var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expiresAtSeconds);
            if (issuedAt > now || notBefore > now || expiresAt <= now || notBefore < issuedAt ||
                expiresAt <= notBefore || expiresAt - issuedAt > TimeSpan.FromSeconds(_settings.MaximumAssertionLifetimeSeconds))
                return false;

            var digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(signedAssertion)));
            assertion = new VerifiedFirmProfessionalDutyAssertion(issuer, audience, assertionId, designationId,
                userId, duty, tenantId, authorityRevision, status, supersedesAssertionId,
                issuedAt, notBefore, expiresAt, digest);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    static bool TryReadObject(JsonElement element, HashSet<string> allowedNames,
        out Dictionary<string, JsonElement> values)
    {
        values = [];
        if (element.ValueKind != JsonValueKind.Object)
            return false;
        foreach (var property in element.EnumerateObject())
        {
            if (!allowedNames.Contains(property.Name) || !values.TryAdd(property.Name, property.Value))
                return false;
        }
        return true;
    }

    static bool StringClaim(Dictionary<string, JsonElement> claims, string name, out string value)
    {
        value = string.Empty;
        if (!claims.TryGetValue(name, out var claim) || claim.ValueKind != JsonValueKind.String)
            return false;
        value = claim.GetString() ?? string.Empty;
        return value.Length > 0 && !value.Any(char.IsControl);
    }

    static bool UuidClaim(Dictionary<string, JsonElement> claims, string name, out Uuid value)
    {
        value = Uuid.Empty;
        return StringClaim(claims, name, out var text) &&
            Uuid.TryParse(text, CultureInfo.InvariantCulture, out value) && value.ToString() == text;
    }

    static bool OptionalUuidClaim(Dictionary<string, JsonElement> claims, string name,
        out Uuid? value)
    {
        value = null;
        if (!claims.TryGetValue(name, out var claim))
            return true;
        if (claim.ValueKind != JsonValueKind.String ||
            !Uuid.TryParse(claim.GetString(), CultureInfo.InvariantCulture, out var parsed) ||
            parsed == Uuid.Empty || parsed.ToString() != claim.GetString())
            return false;
        value = parsed;
        return true;
    }

    static bool IntegerClaim(Dictionary<string, JsonElement> claims, string name, out long value)
    {
        value = 0;
        return claims.TryGetValue(name, out var claim) && claim.ValueKind == JsonValueKind.Number &&
            claim.TryGetInt64(out value);
    }

    static bool SupportedScope(string duty, Dictionary<string, JsonElement> claims, out Uuid? tenantId)
    {
        tenantId = null;
        if (duty == FirmProfessionalDuty.EngagementPartner)
        {
            if (!UuidClaim(claims, "tenant_id", out var tenant) || tenant == Uuid.Empty)
                return false;
            tenantId = tenant;
            return true;
        }
        return duty == FirmProfessionalDuty.RuleRatifier && !claims.ContainsKey("tenant_id");
    }

    static bool TryDecode(string text, out byte[] bytes)
    {
        bytes = [];
        if (text.Length == 0 || text.Any(character =>
                !(character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_')))
            return false;
        var base64 = text.Replace('-', '+').Replace('_', '/');
        base64 += new string('=', (4 - base64.Length % 4) % 4);
        try
        {
            bytes = Convert.FromBase64String(base64);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public void Dispose() => _publicKey?.Dispose();
}

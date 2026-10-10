using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

static class ProfessionalDutyAssertionTestIssuer
{
    public const string Issuer = "https://professional-authority.synthetic.example";
    public const string Audience = "bdgrz.compliance.professional-duty";
    public const string KeyId = "synthetic-issuer-key-1";
    static readonly RSA Key = RSA.Create(2048);
    static readonly object Gate = new();

    public static void AddConfiguration(IDictionary<string, string?> values)
    {
        values["Compliance:ProfessionalAuthority:Issuer"] = Issuer;
        values["Compliance:ProfessionalAuthority:Audience"] = Audience;
        values["Compliance:ProfessionalAuthority:KeyId"] = KeyId;
        lock (Gate)
            values["Compliance:ProfessionalAuthority:PublicKeyBase64"] = Convert.ToBase64String(
                Key.ExportSubjectPublicKeyInfo());
        values["Compliance:ProfessionalAuthority:MaximumAssertionLifetimeSeconds"] = "3600";
    }

    public static SignedAssertion Issue(Uuid designationId, Uuid userId, string duty, Uuid? tenantId,
        long revision, string status, Uuid? supersedes, DateTimeOffset issuedAt, DateTimeOffset expiresAt,
        Uuid? assertionId = null)
    {
        issuedAt = DateTimeOffset.FromUnixTimeSeconds(issuedAt.ToUnixTimeSeconds());
        expiresAt = DateTimeOffset.FromUnixTimeSeconds(expiresAt.ToUnixTimeSeconds());
        var id = assertionId ?? Uuid.CreateVersion4();
        var claims = Claims(designationId, userId, duty, tenantId, revision, status, supersedes,
            issuedAt, expiresAt, id);
        var token = SignClaims(claims);
        var evidence = new VerifiedFirmProfessionalDutyAssertion(Issuer, Audience, id, designationId,
            userId, duty, tenantId, revision, status, supersedes, issuedAt, issuedAt, expiresAt,
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token))));
        return new SignedAssertion(token, evidence);
    }

    public static Dictionary<string, object> Claims(Uuid designationId, Uuid userId, string duty,
        Uuid? tenantId, long revision, string status, Uuid? supersedes, DateTimeOffset issuedAt,
        DateTimeOffset expiresAt, Uuid? assertionId = null)
    {
        issuedAt = DateTimeOffset.FromUnixTimeSeconds(issuedAt.ToUnixTimeSeconds());
        expiresAt = DateTimeOffset.FromUnixTimeSeconds(expiresAt.ToUnixTimeSeconds());
        var id = assertionId ?? Uuid.CreateVersion4();
        var claims = new Dictionary<string, object>
        {
            ["iss"] = Issuer,
            ["aud"] = Audience,
            ["sub"] = userId.ToString(),
            ["jti"] = id.ToString(),
            ["designation_id"] = designationId.ToString(),
            ["duty"] = duty,
            ["authority_revision"] = revision,
            ["status"] = status,
            ["iat"] = issuedAt.ToUnixTimeSeconds(),
            ["nbf"] = issuedAt.ToUnixTimeSeconds(),
            ["exp"] = expiresAt.ToUnixTimeSeconds()
        };
        if (tenantId is { } tenant)
            claims["tenant_id"] = tenant.ToString();
        if (supersedes is { } predecessor)
            claims["supersedes_assertion_id"] = predecessor.ToString();
        return claims;
    }

    public static string SignClaims(Dictionary<string, object> claims, string keyId = KeyId,
        string algorithm = "RS256")
    {
        var header = new Dictionary<string, string>
        {
            ["alg"] = algorithm,
            ["kid"] = keyId,
            ["typ"] = "JWT"
        };
        var encodedHeader = Base64Url(JsonSerializer.SerializeToUtf8Bytes(header));
        var encodedClaims = Base64Url(JsonSerializer.SerializeToUtf8Bytes(claims));
        var input = Encoding.ASCII.GetBytes($"{encodedHeader}.{encodedClaims}");
        byte[] signature;
        lock (Gate)
            signature = Key.SignData(input, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{encodedHeader}.{encodedClaims}.{Base64Url(signature)}";
    }

    static string Base64Url(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    internal sealed record SignedAssertion(string Token, VerifiedFirmProfessionalDutyAssertion Evidence);
}

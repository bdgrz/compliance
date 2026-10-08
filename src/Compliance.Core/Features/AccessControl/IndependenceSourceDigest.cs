using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Bdgrz.Compliance.Features.AccessControl;

static class IndependenceSourceDigest
{
    public static string Acceptance(ServiceEngagementAcceptanceView acceptance) =>
        Digest(acceptance, ComplianceCoreJsonContext.Default.ServiceEngagementAcceptanceView);

    public static string AcceptanceEvent(ServiceEngagementAcceptanceRecorded accepted) =>
        Digest(accepted, ComplianceCoreJsonContext.Default.ServiceEngagementAcceptanceRecorded);

    static string Digest<T>(T value, JsonTypeInfo<T> typeInfo) =>
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, typeInfo)));
}

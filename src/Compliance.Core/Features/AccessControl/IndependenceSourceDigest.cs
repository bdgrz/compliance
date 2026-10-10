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

    public static string DirectorySource(DirectoryStatusSourceView source) =>
        Digest(source, ComplianceCoreJsonContext.Default.DirectoryStatusSourceView);

    public static string DirectoryEvent(FirmStaffChangeRecorded change) =>
        Digest(change, ComplianceCoreJsonContext.Default.FirmStaffChangeRecorded);

    public static string RuleContent(IndependenceRuleContent content) =>
        Digest(content, ComplianceCoreJsonContext.Default.IndependenceRuleContent);

    public static string Services(IReadOnlyList<NonattestServiceView> services) =>
        Digest(services, ComplianceCoreJsonContext.Default.IReadOnlyListNonattestServiceView);

    static string Digest<T>(T value, JsonTypeInfo<T> typeInfo) =>
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, typeInfo)));
}

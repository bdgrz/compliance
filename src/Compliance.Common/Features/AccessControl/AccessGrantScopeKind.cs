using System.Text.Json.Serialization;

namespace Bdgrz.Compliance.Features.AccessControl;

[JsonConverter(typeof(JsonStringEnumConverter<AccessGrantScopeKind>))]
public enum AccessGrantScopeKind
{
    [JsonStringEnumMemberName("organization")]
    Organization,
    [JsonStringEnumMemberName("program")]
    Program,
    [JsonStringEnumMemberName("engagement")]
    Engagement,
    [JsonStringEnumMemberName("shared_resource")]
    SharedResource,
}

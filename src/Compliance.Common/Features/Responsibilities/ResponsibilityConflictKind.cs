using System.Text.Json.Serialization;

namespace Bdgrz.Compliance.Features.Responsibilities;

[JsonConverter(typeof(JsonStringEnumConverter<ResponsibilityConflictKind>))]
public enum ResponsibilityConflictKind
{
    [JsonStringEnumMemberName("self_review")]
    SelfReview,
    [JsonStringEnumMemberName("self_approval")]
    SelfApproval,
}

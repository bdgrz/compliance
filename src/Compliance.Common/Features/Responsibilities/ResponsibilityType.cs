using System.Text.Json.Serialization;

namespace Bdgrz.Compliance.Features.Responsibilities;

[JsonConverter(typeof(JsonStringEnumConverter<ResponsibilityType>))]
public enum ResponsibilityType
{
    [JsonStringEnumMemberName("control_owner")]
    ControlOwner,
    [JsonStringEnumMemberName("evidence_contributor")]
    EvidenceContributor,
    [JsonStringEnumMemberName("assigned_reviewer")]
    AssignedReviewer,
    [JsonStringEnumMemberName("access_reviewer")]
    AccessReviewer,
    [JsonStringEnumMemberName("corrective_action_owner")]
    CorrectiveActionOwner,
    [JsonStringEnumMemberName("policy_approver")]
    PolicyApprover,
}

namespace Bdgrz.Compliance.Features.Responsibilities;

public static class ResponsibilityTypeWireName
{
    public static bool TryParse(string? value, out ResponsibilityType type)
    {
        var parsed = value switch
        {
            "control_owner" => (ResponsibilityType?)ResponsibilityType.ControlOwner,
            "evidence_contributor" => ResponsibilityType.EvidenceContributor,
            "assigned_reviewer" => ResponsibilityType.AssignedReviewer,
            "access_reviewer" => ResponsibilityType.AccessReviewer,
            "corrective_action_owner" => ResponsibilityType.CorrectiveActionOwner,
            "policy_approver" => ResponsibilityType.PolicyApprover,
            _ => null,
        };
        type = parsed.GetValueOrDefault();
        return parsed is not null;
    }
}

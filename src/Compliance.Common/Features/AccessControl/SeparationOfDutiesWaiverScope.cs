using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Identifies the exact conflict a separation-of-duties waiver may cover.</summary>
public sealed record SeparationOfDutiesWaiverScope(
    string RecordType,
    Uuid RecordId,
    Uuid VersionId,
    long Revision,
    string Action);

public static class SeparationOfDutiesRecordTypes
{
    public const string Boundary = "boundary";

    /// <summary>An access-review scope decision; the version is the instance ID.</summary>
    public const string SystemInstanceAccessReviewScope = "system_instance_access_review_scope";

    public const string Commitment = "commitment";

    public const string Control = "control";

    public const string ControlCriterionMapping = "control_criterion_mapping";

    /// <summary>A management readiness decision; the version is the assessment ID.</summary>
    public const string ReadinessAssessment = "readiness_assessment";

    /// <summary>A risk acceptance; the version is the residual assessment ID.</summary>
    public const string Risk = "risk";

    /// <summary>A control treatment assertion; the version is the control version ID.</summary>
    public const string RiskControlTreatment = "risk_control_treatment";

    /// <summary>A criterion applicability decision; the version is the decision ID.</summary>
    public const string CriterionApplicability = "criterion_applicability";
}

public static class SeparationOfDutiesActions
{
    public const string Review = "review";
    public const string Approve = "approve";
}

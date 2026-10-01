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

    /// <summary>
    ///     A control operating plan. A proposal conflict uses the control version and the next plan
    ///     revision; an approval uses the plan version and its revision.
    /// </summary>
    public const string ControlOperatingPlan = "control_operating_plan";

    /// <summary>An occurrence review; the version is the attestation ID and revision its version.</summary>
    public const string ControlOccurrence = "control_occurrence";

    /// <summary>A finding closure; the version is the finding ID and revision the finding revision.</summary>
    public const string Finding = "finding";

    /// <summary>A policy draft or retirement decision; the version is the policy ID.</summary>
    public const string Policy = "policy";

    /// <summary>An access expectation approval; the record and version are the expectation ID and the revision is 1.</summary>
    public const string AccessExpectation = "access_expectation";

    /// <summary>A reviewer's decision on their own access; the record is the item ID, the version the campaign ID, and the revision 1.</summary>
    public const string AccessReviewItem = "access_review_item";

    /// <summary>A control evaluation review; the version is the evaluation ID and revision its round.</summary>
    public const string ControlEvaluation = "control_evaluation";
}

public static class SeparationOfDutiesActions
{
    public const string Review = "review";
    public const string Approve = "approve";
}

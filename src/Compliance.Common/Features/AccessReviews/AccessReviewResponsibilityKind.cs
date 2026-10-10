namespace Bdgrz.Compliance.Features.AccessReviews;

public static class AccessReviewResponsibilityKind
{
    public const string Reviewer = "reviewer";
    public const string RemediationOwner = "remediation_owner";

    public static bool IsKnown(string? responsibility) =>
        responsibility is Reviewer or RemediationOwner;
}

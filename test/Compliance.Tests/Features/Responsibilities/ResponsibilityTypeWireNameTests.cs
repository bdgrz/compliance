using Bdgrz.Compliance.Features.Responsibilities;

namespace Bdgrz.Compliance.Tests.Features.Responsibilities;

public sealed class ResponsibilityTypeWireNameTests
{
    [Theory]
    [InlineData("control_owner", ResponsibilityType.ControlOwner)]
    [InlineData("evidence_contributor", ResponsibilityType.EvidenceContributor)]
    [InlineData("assigned_reviewer", ResponsibilityType.AssignedReviewer)]
    [InlineData("access_reviewer", ResponsibilityType.AccessReviewer)]
    [InlineData("corrective_action_owner", ResponsibilityType.CorrectiveActionOwner)]
    [InlineData("policy_approver", ResponsibilityType.PolicyApprover)]
    public void ShouldParseSnakeCaseResponsibilityTypeGivenKnownWireName(string wireName,
        ResponsibilityType expected)
    {
        // Arrange

        // Act
        var parsed = ResponsibilityTypeWireName.TryParse(wireName, out var actual);

        // Assert
        Assert.True(parsed);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("AssignedReviewer")]
    [InlineData("unknown")]
    [InlineData("")]
    public void ShouldRejectNonWireNameGivenResponsibilityTypeQuery(string wireName)
    {
        // Arrange

        // Act
        var parsed = ResponsibilityTypeWireName.TryParse(wireName, out _);

        // Assert
        Assert.False(parsed);
    }
}

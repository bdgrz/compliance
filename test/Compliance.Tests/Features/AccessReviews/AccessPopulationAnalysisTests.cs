using Bdgrz.Compliance.Features.AccessReviews;

namespace Bdgrz.Compliance.Tests.Features.AccessReviews;

public sealed class AccessPopulationAnalysisTests
{
    [Fact]
    public void ShouldDeriveOneRowWithEveryPathGivenNestedGroupsRoleAssumptionAndDirectGrant()
    {
        // Arrange
        var facts = AccessReviewFixture.StandardFacts() with
        {
            Assignments = [.. AccessReviewFixture.StandardFacts().Assignments, new("ada", "admin")],
        };

        // Act
        var effective = AccessPopulationAnalysis.Derive(facts);

        // Assert
        Assert.Empty(AccessPopulationAnalysis.Validate(facts));
        var adaAdmin = Assert.Single(effective, row => row.ProviderSubjectId == "ada" &&
                                                      row.ProviderEntitlementId == "admin");
        Assert.True(adaAdmin.Direct);
        Assert.Equal(2, adaAdmin.Paths.Count);
        Assert.Contains(adaAdmin.Paths, path => path.Hops.Select(hop => hop.Kind).SequenceEqual(
            ["group_member", "group_member", "access_assignment"]));
        var adaDeploy = Assert.Single(effective, row => row.ProviderSubjectId == "ada" &&
                                                       row.ProviderEntitlementId == "deploy");
        Assert.Equal(["role_assumption", "access_assignment"],
            Assert.Single(adaDeploy.Paths).Hops.Select(hop => hop.Kind));
        Assert.False(adaDeploy.Direct);
        Assert.DoesNotContain(effective, row => row.ProviderSubjectId is "engineering" or
            "admins" or "deployer");
        Assert.Equal(effective.Count, effective.Select(row =>
            (row.ProviderSubjectId, row.ProviderEntitlementId)).Distinct().Count());
    }

    [Fact]
    public void ShouldReportEachIssueCategoryGivenInvalidDuplicateIncompleteAndAmbiguousFacts()
    {
        // Arrange
        var facts = new AccessPopulationFacts(
            [
                new("ada", "user_account", "Ada", "active"),
                new("ada", "user_account", "Ada again", "active"),
                new("ADA", "user_account", "Ada upper", "active"),
                new("x", "printer", "Unknown", "active"),
            ],
            [new("admin", "admin_privilege", "Admin")],
            [new("ada", "x")],
            [new("ghost", "admin"), new("ada", "admin"), new("ada", "admin")]);

        // Act
        var issues = AccessPopulationAnalysis.Validate(facts);

        // Assert
        Assert.Contains(issues, issue => issue is { Category: "duplicate", Code: "duplicate_principal" });
        Assert.Contains(issues, issue => issue is { Category: "ambiguous", Code: "ambiguous_principal_id" });
        Assert.Contains(issues, issue => issue is { Category: "invalid", Code: "unknown_principal_kind" });
        Assert.Contains(issues, issue => issue is { Category: "invalid", Code: "container_not_group_or_role" });
        Assert.Contains(issues, issue => issue is { Category: "incomplete", Code: "unknown_assignment_reference" });
        Assert.Contains(issues, issue => issue is { Category: "duplicate", Code: "duplicate_assignment" });
    }

    [Fact]
    public void ShouldRejectCycleAndEmptyPopulationGivenLoopOrNoPrincipals()
    {
        // Arrange
        var cyclic = new AccessPopulationFacts(
            [
                new("a", "group", "A", "active"), new("b", "group", "B", "active"),
                new("u", "user_account", "U", "active"),
            ],
            [], [new("a", "b"), new("b", "a"), new("a", "u")], []);
        var empty = new AccessPopulationFacts([], [], [], []);

        // Act
        var cycleIssues = AccessPopulationAnalysis.Validate(cyclic);
        var emptyIssues = AccessPopulationAnalysis.Validate(empty);

        // Assert
        Assert.Contains(cycleIssues, issue => issue.Code == "membership_cycle");
        Assert.Contains(emptyIssues, issue => issue is { Category: "incomplete", Code: "no_principals" });
    }

    [Fact]
    public void ShouldKeepCalculationIdStableGivenSameContentHash()
    {
        // Arrange
        var hash = new string('a', 64);

        // Act
        var first = AccessPopulationAnalysis.CalculationId(hash);
        var second = AccessPopulationAnalysis.CalculationId(hash);
        var other = AccessPopulationAnalysis.CalculationId(new string('b', 64));

        // Assert
        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
    }
}

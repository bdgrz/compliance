using Bdgrz.Compliance.Features.AccessControl;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class IndependenceRuleSetTests
{
    [Fact]
    public void ShouldRetainRuleSnapshotGivenSourceListMutation()
    {
        // Arrange
        var rules = new List<IndependenceServiceRule>
        {
            new("control_design", IndependenceServiceClassification.Impairing,
                IndependenceServiceClassification.Impairing),
        };
        var ruleSet = new IndependenceRuleSet(3, 12, rules);
        rules[0] = new("control_design", IndependenceServiceClassification.Compatible,
            IndependenceServiceClassification.Compatible);

        // Act
        var found = ruleSet.TryGetClassification("control_design", false, out var classification);

        // Assert
        Assert.True(found);
        Assert.Equal(IndependenceServiceClassification.Impairing, classification);
        Assert.Equal(3, ruleSet.Version);
    }
}

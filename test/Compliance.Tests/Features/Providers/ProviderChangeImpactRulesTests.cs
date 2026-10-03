using Bdgrz.Compliance.Features.Providers;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Providers;

public sealed class ProviderChangeImpactRulesTests
{
    static readonly DateOnly EffectiveOn = new(2026, 10, 3);

    [Fact]
    public void ShouldExcludeExpiredDependencyGivenTerminationOnExclusiveEndDate()
    {
        // Arrange
        var dependency = Dependency(At(EffectiveOn));

        // Act
        var affected = ProviderChangeImpactRules.IsAffectedByChange(dependency, "termination",
            EffectiveOn);

        // Assert
        Assert.False(affected);
        Assert.Equal("provider_dependency_outside_termination_window",
            ProviderChangeImpactRules.DependencyRelationship("termination", affected));
    }

    [Fact]
    public void ShouldIncludeActiveDependencyGivenTerminationBeforeExclusiveEndDate()
    {
        // Arrange
        var dependency = Dependency(At(EffectiveOn.AddDays(1)));

        // Act
        var affected = ProviderChangeImpactRules.IsAffectedByChange(dependency, "termination",
            EffectiveOn);

        // Assert
        Assert.True(affected);
        Assert.Equal("provider_dependency_active_before_termination",
            ProviderChangeImpactRules.DependencyRelationship("termination", affected));
    }

    [Theory]
    [InlineData("renewal")]
    [InlineData("material_change")]
    public void ShouldExcludeDependencyGivenChangeOnExclusiveEndDate(string changeKind)
    {
        // Arrange
        var dependency = Dependency(At(EffectiveOn));

        // Act
        var affected = ProviderChangeImpactRules.IsAffectedByChange(dependency, changeKind,
            EffectiveOn);

        // Assert
        Assert.False(affected);
        Assert.Equal("provider_dependency_outside_change_date",
            ProviderChangeImpactRules.DependencyRelationship(changeKind, affected));
    }

    static ProviderDependency Dependency(DateTimeOffset? effectiveUntilExclusive) =>
        new("client_service", Uuid.CreateVersion4(), Uuid.CreateVersion4(), null,
            "provides a critical service", At(EffectiveOn.AddDays(-1)),
            effectiveUntilExclusive);

    static DateTimeOffset At(DateOnly date) =>
        new(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
}

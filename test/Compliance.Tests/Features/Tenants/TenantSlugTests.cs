using System.Globalization;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class TenantSlugTests
{
    static readonly Uuid FirstTenant = Uuid.Parse(
        "ee6c0952-f147-4aa4-8b65-f57619e3843d",
        CultureInfo.InvariantCulture);
    static readonly Uuid SecondTenant = Uuid.Parse(
        "cd593d48-e9e5-447d-a1a7-502e94a9b7bd",
        CultureInfo.InvariantCulture);

    [Fact]
    public void ShouldDeriveIdentityGivenNormalizedSlug() =>
        Assert.Equal(new TenantSlug("acme").Id, new TenantSlug("ACME").Id);

    [Fact]
    public void ShouldRejectCompetingTenantGivenConcurrentClaim()
    {
        // Arrange
        var slug = new TenantSlug("acme");
        _ = slug.Register(FirstTenant);
        var scenario = new AggregateScenario<TenantSlug>(slug);

        // Act
        var result = slug.Register(SecondTenant);

        // Assert
        Assert.True(result.IsSuccess);
        var rejectedEvent = scenario.PendingEvents[^1];
        Assert.Equal("TenantSlugRegistrationRejected", rejectedEvent.GetType().Name);
        Assert.Equal(SecondTenant, rejectedEvent.GetType().GetProperty("TenantId")?.GetValue(rejectedEvent));
    }

    [Fact]
    public void ShouldAllowSurrenderGivenOwningTenant()
    {
        // Arrange
        var slug = new TenantSlug("acme");
        _ = slug.Register(FirstTenant);
        var scenario = new AggregateScenario<TenantSlug>(slug);

        var rejected = slug.Surrender(SecondTenant);

        // Act
        var surrendered = slug.Surrender(FirstTenant);

        // Assert
        Assert.True(rejected.IsSuccess);
        Assert.IsType<TenantSlugSurrenderRejected>(scenario.PendingEvents[^2]);
        Assert.True(surrendered.IsSuccess);
        Assert.IsType<TenantSlugSurrendered>(scenario.PendingEvents[^1]);
    }

    // Regression coverage for the backlog's "a retired slug is never assigned to another
    // organization" rule: surrendering must permanently spend a slug, not free it for reuse.
    [Fact]
    public void ShouldStayReservedGivenSurrenderedSlug()
    {
        // Arrange
        var slug = new TenantSlug("acme");
        _ = slug.Register(FirstTenant);

        // Act
        _ = slug.Surrender(FirstTenant);

        // Assert
        Assert.False(slug.IsAvailableFor(FirstTenant));
        Assert.False(slug.IsAvailableFor(SecondTenant));
        Assert.Equal(FirstTenant, slug.OwningTenantId);
        Assert.True(slug.IsRetired);
    }

    [Fact]
    public void ShouldRejectRegistrationGivenRetiredSlug()
    {
        // Arrange
        var slug = new TenantSlug("acme");
        _ = slug.Register(FirstTenant);
        _ = slug.Surrender(FirstTenant);
        var scenario = new AggregateScenario<TenantSlug>(slug);

        // Act
        var result = slug.Register(FirstTenant);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.IsType<TenantSlugRegistrationRejected>(scenario.PendingEvents[^1]);
    }

    [Fact]
    public void ShouldRejectNewClaimGivenHistoricalSlugLookup()
    {
        // Arrange
        var slug = new TenantSlug("login", allowReserved: true);
        var scenario = new AggregateScenario<TenantSlug>(slug);

        // Act
        var result = slug.Register(FirstTenant);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(slug.OwningTenantId);
        Assert.IsType<TenantSlugRegistrationRejected>(scenario.PendingEvents[^1]);
    }
}

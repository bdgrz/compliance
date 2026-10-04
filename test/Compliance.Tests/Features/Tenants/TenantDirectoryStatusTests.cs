using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class TenantDirectoryStatusTests
{
    [Fact]
    public void ShouldProjectOffboardingStatusGivenAccessRevocationStarts()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var current = new TenantView(tenantId, "Acme", "acme");
        var ev = new TenantOffboardingStarted(tenantId, Uuid.CreateVersion4(),
            "Client requested offboarding", DateTimeOffset.UnixEpoch);

        // Act
        var status = TenantDirectoryStatus.After(current, ev);

        // Assert
        Assert.Equal("offboarding", status);
    }

    [Fact]
    public void ShouldKeepOffboardingStatusGivenActivationOrReactivationEvents()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var current = new TenantView(tenantId, "Acme", "acme", Status: "offboarding");
        var activated = new TenantActivated(tenantId, Uuid.CreateVersion4());
        var reactivated = new TenantReactivated(tenantId, Uuid.CreateVersion4());

        // Act
        var activationStatus = TenantDirectoryStatus.After(current, activated);
        var reactivationStatus = TenantDirectoryStatus.After(current, reactivated);

        // Assert
        Assert.Equal("offboarding", activationStatus);
        Assert.Equal("offboarding", reactivationStatus);
    }
}

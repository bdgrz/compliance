using System.Globalization;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class TenantOwnerTests
{
    [Fact]
    public void ShouldRegisterOnceGivenRepeatedOwnerRelationship()
    {
        // Arrange
        var tenantId = Uuid.Parse("46ca06ed-bddb-4283-9482-b8667ed89e86", CultureInfo.InvariantCulture);
        var userId = Uuid.Parse("0862062f-97e9-45de-a312-0f884c48180d", CultureInfo.InvariantCulture);
        var owner = new TenantOwner(tenantId, userId);
        var sameOwner = new TenantOwner(tenantId, userId);
        var scenario = new AggregateScenario<TenantOwner>(owner);

        var registered = owner.Register();

        // Act
        var repeated = owner.Register();

        // Assert
        Assert.True(registered.IsSuccess);
        Assert.True(repeated.IsSuccess);
        Assert.Equal(owner.Id, sameOwner.Id);
        var tenantOwnerRegistered = Assert.Single(scenario.PendingEvents);
        Assert.Equal("TenantOwnerRegistered", tenantOwnerRegistered.GetType().Name);
    }
}

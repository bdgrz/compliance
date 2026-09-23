using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Bdgrz.Compliance;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class TenantContractCompatibilityTests
{
    [Fact]
    public void ShouldRemainActiveGivenLegacyRegistrationWithoutInvitation()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var operatorId = Uuid.CreateVersion4();
        var json = $$"""{"tenant_id":"{{tenantId}}","owner_user_id":"{{operatorId}}","name":"Acme","slug":"acme"}""";

        // Act
        var registered = JsonSerializer.Deserialize(json,
            (JsonTypeInfo)ComplianceCoreJsonContext.Default.TenantRegistered);

        // Assert
        Assert.NotNull(registered);
        Assert.Equal("TenantRegistered", registered.GetType().Name);
        Assert.Null(registered.GetType().GetProperty("FirstAdministratorEmail")?.GetValue(registered));
        Assert.False((bool)registered.GetType().GetProperty("CreatorIsAdministrator")!
            .GetValue(registered)!);
    }

    [Fact]
    public void ShouldDefaultToActiveGivenLegacyDirectoryRow()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var json = $$"""{"tenant_id":"{{tenantId}}","name":"Acme","slug":"acme"}""";

        // Act
        var view = JsonSerializer.Deserialize(json, ComplianceCoreJsonContext.Default.TenantView);

        // Assert
        Assert.Equal("active", view?.Status);
    }

    [Fact]
    public void ShouldDefaultToClientPersonnelGivenLegacyMembershipEvent()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var memberId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var json = $$"""{"tenant_id":"{{tenantId}}","member_id":"{{memberId}}","user_id":"{{userId}}"}""";

        // Act
        var registered = JsonSerializer.Deserialize(json,
            (JsonTypeInfo)ComplianceCoreJsonContext.Default.MemberRegistered);

        // Assert
        Assert.NotNull(registered);
        Assert.Equal("client_personnel",
            registered.GetType().GetProperty("Affiliation")?.GetValue(registered));
    }
}

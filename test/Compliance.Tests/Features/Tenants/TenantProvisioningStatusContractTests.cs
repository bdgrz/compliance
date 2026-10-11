using System.Text.Json;
using Bdgrz.Compliance;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class TenantProvisioningStatusContractTests
{
    [Theory]
    [InlineData("provisioning", null)]
    [InlineData("completed", null)]
    [InlineData("rejected", "slug_unavailable")]
    public void ShouldExposeOnlyBoundedSnakeCaseFieldsGivenProvisioningStatusSerialization(
        string status, string? failureCode)
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var view = new TenantProvisioningStatusView(tenantId, status, failureCode);

        // Act
        var json = JsonSerializer.Serialize(view, ComplianceCoreJsonContext.Default.TenantProvisioningStatusView);
        using var document = JsonDocument.Parse(json);
        var roundTrip = JsonSerializer.Deserialize(json, ComplianceCoreJsonContext.Default.TenantProvisioningStatusView);

        // Assert
        Assert.Equal(["tenant_id", "status", "failure_code"],
            document.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal(tenantId.ToString(), document.RootElement.GetProperty("tenant_id").GetString());
        Assert.Equal(status, document.RootElement.GetProperty("status").GetString());
        Assert.Equal(failureCode, document.RootElement.GetProperty("failure_code").GetString());
        Assert.Equal(view, roundTrip);
    }

    [Fact]
    public void ShouldSerializeTenantIdGivenProvisioningStatusRequest()
    {
        // Arrange
        var request = new GetTenantProvisioningStatus(Uuid.CreateVersion4());

        // Act
        var json = JsonSerializer.Serialize(request, ComplianceCoreJsonContext.Default.GetTenantProvisioningStatus);
        using var document = JsonDocument.Parse(json);

        // Assert
        Assert.Equal("tenant_id", Assert.Single(document.RootElement.EnumerateObject()).Name);
        Assert.Equal(request, JsonSerializer.Deserialize(json,
            ComplianceCoreJsonContext.Default.GetTenantProvisioningStatus));
    }
}

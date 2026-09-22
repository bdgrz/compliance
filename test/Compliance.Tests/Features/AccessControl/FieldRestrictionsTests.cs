using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class FieldRestrictionsTests
{
    static readonly Uuid TenantId = Id("8d0b7c3a-4e1f-4a52-9d61-0c7e5f2a9b01");
    static readonly Uuid MemberId = Id("8d0b7c3a-4e1f-4a52-9d61-0c7e5f2a9b02");

    [Fact]
    public void ShouldNameReadPermissionGivenQuarantinedEvidenceContentClass()
    {
        // Arrange
        var fieldClass = FieldClasses.EvidenceQuarantinedContent;

        // Act
        var permission = fieldClass.ReadPermission;

        // Assert
        Assert.Equal("field.evidence.quarantined_content.read", permission);
    }

    [Fact]
    public async Task ShouldRedactFieldGivenActorWithoutFieldClassPermission()
    {
        // Arrange
        var permissions = new FixedPermissions();

        // Act
        var field = await FieldRestrictions.ReadAsync(permissions, TenantId, MemberId,
            FieldClasses.EvidenceQuarantinedContent, "raw quarantined bytes");

        // Assert
        Assert.True(field.IsRedacted);
        Assert.Null(field.Value);
    }

    [Fact]
    public async Task ShouldReturnFieldGivenActorWithFieldClassPermission()
    {
        // Arrange
        var permissions = new FixedPermissions(FieldClasses.EvidenceQuarantinedContent.ReadPermission);

        // Act
        var field = await FieldRestrictions.ReadAsync(permissions, TenantId, MemberId,
            FieldClasses.EvidenceQuarantinedContent, "raw quarantined bytes");

        // Assert
        Assert.False(field.IsRedacted);
        Assert.Equal("raw quarantined bytes", field.Value);
    }

    sealed class FixedPermissions(params string[] granted) : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid memberId, string permission,
            CancellationToken ct = default) =>
            ValueTask.FromResult(tenantId == TenantId && memberId == MemberId && granted.Contains(permission));
    }

    static Uuid Id(string value) => Uuid.Parse(value, CultureInfo.InvariantCulture);
}

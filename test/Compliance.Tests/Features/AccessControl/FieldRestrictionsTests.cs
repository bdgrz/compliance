using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class FieldRestrictionsTests
{
    static readonly Uuid TenantId = Id("8d0b7c3a-4e1f-4a52-9d61-0c7e5f2a9b01");
    static readonly Uuid UserId = Id("8d0b7c3a-4e1f-4a52-9d61-0c7e5f2a9b02");

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
        var redactor = await FieldRestrictions.ForActorAsync(permissions, TenantId, UserId,
            FieldClasses.EvidenceQuarantinedContent);
        var field = redactor.Apply("raw quarantined bytes");

        // Assert
        Assert.True(field.IsRedacted);
        Assert.Throws<InvalidOperationException>(() => _ = field.Value);
    }

    [Fact]
    public async Task ShouldReturnFieldGivenActorWithFieldClassPermission()
    {
        // Arrange
        var permissions = new FixedPermissions(FieldClasses.EvidenceQuarantinedContent.ReadPermission);

        // Act
        var redactor = await FieldRestrictions.ForActorAsync(permissions, TenantId, UserId,
            FieldClasses.EvidenceQuarantinedContent);
        var field = redactor.Apply("raw quarantined bytes");

        // Assert
        Assert.False(field.IsRedacted);
        Assert.Equal("raw quarantined bytes", field.Value);
    }

    [Fact]
    public async Task ShouldCheckPermissionOnceGivenManyValues()
    {
        // Arrange
        var permissions = new FixedPermissions(FieldClasses.EvidenceQuarantinedContent.ReadPermission);

        // Act
        var redactor = await FieldRestrictions.ForActorAsync(permissions, TenantId, UserId,
            FieldClasses.EvidenceQuarantinedContent);
        var fields = Enumerable.Range(0, 100).Select(value => redactor.Apply(value)).ToArray();

        // Assert
        Assert.Equal(1, permissions.Checks);
        Assert.All(fields, field => Assert.False(field.IsRedacted));
    }

    [Fact]
    public async Task ShouldNotExposeDefaultValueGivenRedactedValueType()
    {
        // Arrange
        var permissions = new FixedPermissions();

        // Act
        var redactor = await FieldRestrictions.ForActorAsync(permissions, TenantId, UserId,
            FieldClasses.EvidenceQuarantinedContent);
        var field = redactor.Apply(125_000m);

        // Assert
        Assert.True(field.IsRedacted);
        Assert.Throws<InvalidOperationException>(() => _ = field.Value);
    }

    sealed class FixedPermissions(params string[] granted) : IPermissionAuthorizer
    {
        public int Checks { get; private set; }

        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission,
            CancellationToken ct = default)
        {
            Checks++;
            return ValueTask.FromResult(tenantId == TenantId && userId == UserId &&
                memberId == RbacIds.Member(TenantId, UserId) &&
                granted.Contains(permission));
        }
    }

    static Uuid Id(string value) => Uuid.Parse(value, CultureInfo.InvariantCulture);
}

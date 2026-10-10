using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class FirmProfessionalDutyCatalogTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldGrantOnlyExplicitTenantScopedEngagementPartnerDutyGivenDirectoryStaffMetadata()
    {
        // Arrange
        var catalog = new FirmProfessionalDutyCatalog();
        var staff = Staff();

        // Act
        var designated = catalog.Designate(Uuid.CreateVersion4(), Uuid.CreateVersion4(), staff,
            FirmProfessionalDuty.EngagementPartner, Tenant, "Synthetic external duty evidence", 0,
            Operator(), Now);

        // Assert
        Assert.True(designated.IsSuccess, designated.Error?.Message);
        Assert.True(catalog.IsDesignated(staff.UserId, FirmProfessionalDuty.EngagementPartner, Tenant, Now));
        Assert.False(catalog.IsDesignated(staff.UserId, FirmProfessionalDuty.EngagementPartner,
            Uuid.CreateVersion4(), Now));
        Assert.False(catalog.IsDesignated(staff.UserId, FirmProfessionalDuty.RuleRatifier, Tenant, Now));
        Assert.False(new FirmProfessionalDutyCatalog().IsDesignated(staff.UserId,
            FirmProfessionalDuty.EngagementPartner, Tenant, Now));
    }

    [Fact]
    public void ShouldRetainRevocationAndRejectStaleDutyMutationGivenReplayedRequests()
    {
        // Arrange
        var catalog = new FirmProfessionalDutyCatalog();
        var designationId = Uuid.CreateVersion4();
        var staff = Staff();
        var designated = catalog.Designate(Uuid.CreateVersion4(), designationId, staff,
            FirmProfessionalDuty.EngagementPartner, Tenant, "Synthetic external duty evidence", 0,
            Operator(), Now);
        var hadAuthority = catalog.IsDesignated(staff.UserId, FirmProfessionalDuty.EngagementPartner, Tenant, Now);

        // Act
        var revoked = catalog.Revoke(Uuid.CreateVersion4(), designationId, 1,
            "Synthetic designation ended", Operator(), Now.AddMinutes(1));
        var stale = catalog.Revoke(Uuid.CreateVersion4(), designationId, 1,
            "Stale retry", Operator(), Now.AddMinutes(2));

        // Assert
        Assert.True(designated.IsSuccess, designated.Error?.Message);
        Assert.True(revoked.IsSuccess, revoked.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.True(hadAuthority);
        Assert.False(catalog.IsDesignated(staff.UserId, FirmProfessionalDuty.EngagementPartner, Tenant,
            Now.AddMinutes(1)));
        var retained = Assert.Single(catalog.Designations);
        Assert.False(retained.IsActive);
        Assert.Equal(2, retained.Revision);
    }

    [Theory]
    [InlineData(FirmProfessionalDuty.EngagementPartner, null)]
    [InlineData(FirmProfessionalDuty.RuleRatifier, "tenant")]
    public void ShouldRejectInvalidDutyScopeGivenGlobalAndClientDuties(string duty, string? scope)
    {
        // Arrange
        var catalog = new FirmProfessionalDutyCatalog();
        Uuid? tenantId = scope is null ? null : Tenant;

        // Act
        var result = catalog.Designate(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Staff(), duty,
            tenantId, "Synthetic external duty evidence", 0, Operator(), Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(catalog.Designations);
    }

    static FirmStaffMemberView Staff() => new(Uuid.CreateVersion4(), Uuid.CreateVersion4(), "attest",
        "Synthetic staff source", true, 4, Operator(), Now);

    static ActorReference Operator() => ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Synthetic operator");
}

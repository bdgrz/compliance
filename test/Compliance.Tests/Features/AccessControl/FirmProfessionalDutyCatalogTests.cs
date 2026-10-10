using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
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
        var assertion = ProfessionalDutyAssertionTestIssuer.Issue(Uuid.CreateVersion4(), staff.UserId,
            FirmProfessionalDuty.EngagementPartner, Tenant, 1, "active", null, Now, Now.AddHours(1));
        var designated = catalog.Designate(Uuid.CreateVersion4(), assertion.Evidence, assertion.Token, staff,
            0, Operator(), Now);

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
        var assertion = ProfessionalDutyAssertionTestIssuer.Issue(designationId, staff.UserId,
            FirmProfessionalDuty.EngagementPartner, Tenant, 1, "active", null, Now, Now.AddHours(1));
        var designated = catalog.Designate(Uuid.CreateVersion4(), assertion.Evidence, assertion.Token, staff,
            0, Operator(), Now);
        var hadAuthority = catalog.IsDesignated(staff.UserId, FirmProfessionalDuty.EngagementPartner, Tenant, Now);

        // Act
        var revocation = ProfessionalDutyAssertionTestIssuer.Issue(designationId, staff.UserId,
            FirmProfessionalDuty.EngagementPartner, Tenant, 2, "revoked", assertion.Evidence.AssertionId,
            Now.AddMinutes(1), Now.AddHours(1));
        var revoked = catalog.Revoke(Uuid.CreateVersion4(), revocation.Evidence, revocation.Token, 1,
            Operator(), Now.AddMinutes(1));
        var stale = catalog.Revoke(Uuid.CreateVersion4(), revocation.Evidence, revocation.Token, 1,
            Operator(), Now.AddMinutes(2));

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

    [Fact]
    public void ShouldRequireExactSuccessorAndRejectReplayedAssertionGivenDutyRefresh()
    {
        // Arrange
        var catalog = new FirmProfessionalDutyCatalog();
        var designationId = Uuid.CreateVersion4();
        var staff = Staff();
        var first = ProfessionalDutyAssertionTestIssuer.Issue(designationId, staff.UserId,
            FirmProfessionalDuty.EngagementPartner, Tenant, 1, "active", null, Now, Now.AddHours(1));
        var firstWrite = catalog.Designate(Uuid.CreateVersion4(), first.Evidence, first.Token, staff,
            catalog.Sequence, Operator(), Now);
        var successor = ProfessionalDutyAssertionTestIssuer.Issue(designationId, staff.UserId,
            FirmProfessionalDuty.EngagementPartner, Tenant, 2, "active", first.Evidence.AssertionId,
            Now.AddMinutes(1), Now.AddHours(1));

        // Act
        var refreshed = catalog.Designate(Uuid.CreateVersion4(), successor.Evidence, successor.Token, staff,
            catalog.Sequence, Operator(), Now.AddMinutes(1));
        var replayed = catalog.Designate(Uuid.CreateVersion4(), successor.Evidence, successor.Token, staff,
            catalog.Sequence, Operator(), Now.AddMinutes(2));

        // Assert
        Assert.True(firstWrite.IsSuccess, firstWrite.Error?.Message);
        Assert.True(refreshed.IsSuccess, refreshed.Error?.Message);
        Assert.Equal(2, refreshed.Value!.Revision);
        Assert.Equal(firstWrite.Value!.DesignatedAt, refreshed.Value.DesignatedAt);
        Assert.Equal("Verified authority assertion refreshed", refreshed.Value.ChangeReason);
        Assert.Equal(RequestErrorKind.Conflict, replayed.Error?.Kind);
        Assert.Equal(2, catalog.Sequence);
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
        var staff = Staff();
        var assertion = ProfessionalDutyAssertionTestIssuer.Issue(Uuid.CreateVersion4(), staff.UserId, duty,
            tenantId, 1, "active", null, Now, Now.AddHours(1));
        var result = catalog.Designate(Uuid.CreateVersion4(), assertion.Evidence, assertion.Token, staff,
            0, Operator(), Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(catalog.Designations);
    }

    static FirmStaffMemberView Staff() => new(Uuid.CreateVersion4(), Uuid.CreateVersion4(), "attest",
        "Synthetic staff source", true, 4, Operator(), Now);

    static ActorReference Operator() => ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Synthetic operator");
}

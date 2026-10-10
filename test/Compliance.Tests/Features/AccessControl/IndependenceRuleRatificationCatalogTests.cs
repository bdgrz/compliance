using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class IndependenceRuleRatificationCatalogTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRetainExactMonotonicRatificationGivenExplicitRuleRatifierDesignation()
    {
        // Arrange
        var staff = new FirmStaffMemberView(Uuid.CreateVersion4(), Uuid.CreateVersion4(), "attest",
            "Synthetic staff source", true, 4,
            ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Synthetic operator"), Now);
        var duty = new FirmProfessionalDutyDesignationView(Uuid.CreateVersion4(), staff.StaffMemberId,
            staff.UserId, FirmProfessionalDuty.RuleRatifier, null, "Synthetic duty evidence", staff.Revision,
            true, 1, ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Synthetic operator"), Now,
            ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Synthetic operator"), Now, null);
        var actor = new ActorReference("firm_staff", staff.UserId.ToString(), "Synthetic ratifier");
        var catalog = new IndependenceRuleRatificationCatalog();
        var digestV1 = new string('a', 64);
        var digestV2 = new string('b', 64);

        // Act
        var first = catalog.Ratify(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, 1, 0,
            digestV1, "Synthetic ratification source", staff, duty, actor, Now);
        var repeatedVersion = catalog.Ratify(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, 1, 1,
            digestV1, "Synthetic repeat", staff, duty, actor, Now.AddMinutes(1));
        var next = catalog.Ratify(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 2, 2, 1,
            digestV2, "Synthetic next ratification", staff, duty, actor, Now.AddMinutes(2));

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, repeatedVersion.Error?.Kind);
        Assert.True(next.IsSuccess, next.Error?.Message);
        Assert.Equal(2, catalog.Sequence);
        Assert.Equal(2, catalog.Active!.RuleVersion);
        Assert.Equal(digestV2, catalog.Active.RuleContentDigest);
        Assert.Equal(duty.DesignationId, catalog.Active.DutyDesignationId);
        Assert.Equal(staff.Revision, catalog.Active.DirectoryStaffRevision);
    }
}

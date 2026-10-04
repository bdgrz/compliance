using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class NonattestServiceRecordTests
{
    [Fact]
    public void ShouldPreserveStaffSnapshotGivenSourceListMutation()
    {
        // Arrange
        var staffMemberId = Uuid.CreateVersion4();
        var staff = new List<Uuid> { staffMemberId };
        var record = new NonattestServiceRecord(Uuid.CreateVersion4(), Uuid.CreateVersion4(), "readiness",
            new DateOnly(2026, 1, 1), null, staff, false);

        // Act
        staff.Clear();

        // Assert
        Assert.Equal([staffMemberId], record.FirmStaffMemberIds);
    }
}

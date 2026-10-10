using Bdgrz.Compliance.Features.Work;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class WorkDigestScheduleTests
{
    [Fact]
    public void ShouldWaitUntilMemberLocalMondayAtNineGivenNewYorkTimeZone()
    {
        // Arrange
        var beforeSchedule = new DateTimeOffset(2026, 10, 5, 12, 59, 0, TimeSpan.Zero);
        var atSchedule = new DateTimeOffset(2026, 10, 5, 13, 0, 0, TimeSpan.Zero);

        // Act
        var before = WorkDigestSchedule.TryGetDue(beforeSchedule, "America/New_York", out _);
        var due = WorkDigestSchedule.TryGetDue(atSchedule, "America/New_York", out var window);

        // Assert
        Assert.False(before);
        Assert.True(due);
        Assert.Equal(new DateOnly(2026, 10, 5), window.WeekOf);
        Assert.Equal("America/New_York", window.TimeZoneId);
        Assert.Equal(atSchedule, window.ScheduledAt);
    }

    [Fact]
    public void ShouldUseUtcGivenMemberTimeZoneIsMissingOrInvalidWhenDueWeek()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);

        // Act
        var missingZoneDue = WorkDigestSchedule.TryGetDue(now, null, out var missingZone);
        var invalidZoneDue = WorkDigestSchedule.TryGetDue(now, "Not/A_Real_Zone", out var invalidZone);

        // Assert
        Assert.True(missingZoneDue);
        Assert.True(invalidZoneDue);
        Assert.Equal("UTC", missingZone.TimeZoneId);
        Assert.Equal("UTC", invalidZone.TimeZoneId);
        Assert.Equal(now, invalidZone.ScheduledAt);
    }
}

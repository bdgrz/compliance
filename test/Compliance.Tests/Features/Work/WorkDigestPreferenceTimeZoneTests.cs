using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Work;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class WorkDigestPreferenceTimeZoneTests
{
    [Fact]
    public void ShouldRetainMemberTimeZoneGivenEmailPreferenceChangeWithoutTimeZone()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var memberId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(memberId, "member");
        var preference = new WorkDigestPreference(tenantId, memberId);
        var at = new DateTimeOffset(2026, 10, 5, 13, 0, 0, TimeSpan.Zero);
        preference.Set(true, "America/New_York", actor, at);

        // Act
        preference.Set(false, null, actor, at.AddMinutes(1));
        var view = preference.Read();

        // Assert
        Assert.False(view.EmailDigestEnabled);
        Assert.Equal("America/New_York", view.TimeZoneId);
        Assert.Equal(at.AddMinutes(1), view.ChangedAt);
    }

    [Fact]
    public void ShouldDefaultOlderPreferenceEventToUtcGivenMissingTimeZoneField()
    {
        // Arrange
        var ev = new WorkDigestPreferenceChanged(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            true, ActorReference.ForMember(Uuid.CreateVersion4(), "member"),
            DateTimeOffset.UnixEpoch);

        // Act
        var timeZoneId = ev.TimeZoneId;

        // Assert
        Assert.Equal("UTC", timeZoneId);
    }
}

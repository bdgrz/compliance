using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Features.Operations;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class WorkReminderTests
{
    [Fact]
    public async Task ShouldRemindOncePerItemGivenDueAndOverdueWork()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId, fixture.Today,
            fixture.Today.AddDays(-2), fixture.Today.AddDays(4));

        // Act
        var first = await fixture.AsAsync(fixture.OwnerUserId,
            new ListWorkReminders(fixture.TenantId, fixture.ProgramId));
        var second = await fixture.AsAsync(fixture.OwnerUserId,
            new ListWorkReminders(fixture.TenantId, fixture.ProgramId));

        // Assert
        Assert.Equal(["overdue", "due"], first.Select(reminder => reminder.Kind));
        Assert.Equal(2, first[0].DaysOverdue);
        Assert.Equal(first.Select(reminder => reminder.ReminderId),
            second.Select(reminder => reminder.ReminderId));
        Assert.Empty(await fixture.AsAsync(fixture.OutsiderUserId,
            new ListWorkReminders(fixture.TenantId, fixture.ProgramId)));
    }

    [Fact]
    public async Task ShouldRemindProgramManagerGivenSystemEscalation()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId, fixture.Today.AddDays(-9));

        // Act
        var reminders = await fixture.AsAsync(fixture.LeadUserId,
            new ListWorkReminders(fixture.TenantId, fixture.ProgramId));

        // Assert
        var reminder = Assert.Single(reminders);
        Assert.Equal("escalated", reminder.Kind);
        Assert.Equal(9, reminder.DaysOverdue);
    }

    [Fact]
    public async Task ShouldDigestOverdueAndNextSevenDaysGivenMixedDueDates()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId,
            fixture.Today.AddDays(-1), fixture.Today.AddDays(5), fixture.Today.AddDays(20));

        // Act
        var digest = await fixture.AsAsync(fixture.OwnerUserId,
            new GetWorkDigest(fixture.TenantId, fixture.ProgramId));

        // Assert
        Assert.Equal(fixture.Today.AddDays(-1), Assert.Single(digest.Overdue).DueOn);
        Assert.Equal(fixture.Today.AddDays(5), Assert.Single(digest.DueSoon).DueOn);
        Assert.Equal(DayOfWeek.Monday, digest.WeekOf.DayOfWeek);
        Assert.True(digest.EmailDigestEnabled);
        var again = await fixture.AsAsync(fixture.OwnerUserId,
            new GetWorkDigest(fixture.TenantId, fixture.ProgramId));
        Assert.Equal(digest.DigestId, again.DigestId);
    }

    [Fact]
    public async Task ShouldKeepInAppRemindersGivenEmailDigestOptOut()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId, fixture.Today);

        // Act
        var preference = await fixture.AsAsync(fixture.OwnerUserId,
            new SetWorkDigestPreference(fixture.TenantId, false));

        // Assert
        Assert.False(preference.EmailDigestEnabled);
        Assert.False((await fixture.AsAsync(fixture.OwnerUserId,
            new GetWorkDigestPreference(fixture.TenantId))).EmailDigestEnabled);
        Assert.True((await fixture.AsAsync(fixture.LeadUserId,
            new GetWorkDigestPreference(fixture.TenantId))).EmailDigestEnabled);
        Assert.False((await fixture.AsAsync(fixture.OwnerUserId,
            new GetWorkDigest(fixture.TenantId, fixture.ProgramId))).EmailDigestEnabled);
        Assert.Single(await fixture.AsAsync(fixture.OwnerUserId,
            new ListWorkReminders(fixture.TenantId, fixture.ProgramId)));
    }
}

using Bdgrz.Compliance.Features.Work;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class WorkDigestDispatchTests
{
    [Fact]
    public void ShouldFreezeScheduleAndMessageIdentityGivenRepeatedWeeklyClaim()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var memberId = Uuid.CreateVersion4();
        var messageId = Uuid.CreateVersion5(memberId, "digest:2026-10-05");
        var schedule = new WorkDigestScheduleWindow(new DateOnly(2026, 10, 5),
            "America/New_York", new DateTimeOffset(2026, 10, 5, 13, 0, 0, TimeSpan.Zero));
        var dispatch = new WorkDigestDispatch(tenantId, memberId);

        // Act
        var firstClaim = dispatch.Schedule(schedule, messageId, TimeSpan.FromDays(6),
            new DateTimeOffset(2026, 10, 5, 13, 1, 0, TimeSpan.Zero));
        var replayClaim = dispatch.Schedule(schedule with { TimeZoneId = "UTC" },
            Uuid.CreateVersion4(), TimeSpan.FromDays(6),
            new DateTimeOffset(2026, 10, 5, 13, 2, 0, TimeSpan.Zero));
        var status = dispatch.Read(schedule.WeekOf);

        // Assert
        Assert.True(firstClaim);
        Assert.False(replayClaim);
        Assert.Equal("scheduled", status.Status);
        Assert.Equal("America/New_York", status.TimeZoneId);
        Assert.Equal(schedule.ScheduledAt, status.ScheduledAt);
        Assert.Equal(messageId, status.MessageId);
    }

    [Fact]
    public void ShouldRetryOnlyDefiniteTransientRejectionGivenDispatchAttempt()
    {
        // Arrange
        var schedule = new WorkDigestScheduleWindow(new DateOnly(2026, 10, 5), "UTC",
            new DateTimeOffset(2026, 10, 5, 9, 0, 0, TimeSpan.Zero));
        var now = schedule.ScheduledAt;
        var dispatch = new WorkDigestDispatch(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        Assert.True(dispatch.Schedule(schedule, Uuid.CreateVersion4(), TimeSpan.FromHours(24), now));

        // Act
        var started = dispatch.StartAttempt(schedule.WeekOf, now);
        dispatch.RecordTransientRejection(schedule.WeekOf, now, maximumAttempts: 3,
            retryDelay: TimeSpan.FromMinutes(5));
        var retry = dispatch.Read(schedule.WeekOf);
        var nextAttemptStarted = dispatch.StartAttempt(schedule.WeekOf, retry.NextAttemptAt!.Value);
        dispatch.RecordUnknown(schedule.WeekOf, retry.NextAttemptAt.Value.AddSeconds(1),
            "transport_ambiguous");
        var unresolved = dispatch.Read(schedule.WeekOf);
        var duplicate = dispatch.StartAttempt(schedule.WeekOf, retry.NextAttemptAt.Value.AddMinutes(10));

        // Assert
        Assert.True(started);
        Assert.True(nextAttemptStarted);
        Assert.Equal("retry_pending", retry.Status);
        Assert.Equal(1, retry.Attempts);
        Assert.Equal(now.AddMinutes(5), retry.NextAttemptAt);
        Assert.Equal("unknown", unresolved.Status);
        Assert.False(duplicate);
    }

    [Fact]
    public void ShouldExposeInterruptedAttemptAsUnknownGivenWorkerRestart()
    {
        // Arrange
        var schedule = new WorkDigestScheduleWindow(new DateOnly(2026, 10, 5), "UTC",
            new DateTimeOffset(2026, 10, 5, 9, 0, 0, TimeSpan.Zero));
        var dispatch = new WorkDigestDispatch(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        Assert.True(dispatch.Schedule(schedule, Uuid.CreateVersion4(), TimeSpan.FromDays(6),
            schedule.ScheduledAt));
        Assert.True(dispatch.StartAttempt(schedule.WeekOf, schedule.ScheduledAt));

        // Act
        dispatch.RecordInterruptedAttempt(schedule.WeekOf,
            schedule.ScheduledAt.AddMinutes(1));
        var status = dispatch.Read(schedule.WeekOf);
        var retry = dispatch.StartAttempt(schedule.WeekOf, schedule.ScheduledAt.AddMinutes(2));

        // Assert
        Assert.Equal("unknown", status.Status);
        Assert.Equal("worker_interrupted", status.FailureCode);
        Assert.False(retry);
    }

    [Fact]
    public void ShouldPersistEligibilitySkipWithoutStartingSmtpAttemptGivenOptOut()
    {
        // Arrange
        var schedule = new WorkDigestScheduleWindow(new DateOnly(2026, 10, 5), "UTC",
            new DateTimeOffset(2026, 10, 5, 9, 0, 0, TimeSpan.Zero));
        var dispatch = new WorkDigestDispatch(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        Assert.True(dispatch.Schedule(schedule, Uuid.CreateVersion4(), TimeSpan.FromDays(6),
            schedule.ScheduledAt));

        // Act
        var skipped = dispatch.RecordSkipped(schedule.WeekOf,
            schedule.ScheduledAt.AddMinutes(1), "email_opt_out");
        var status = dispatch.Read(schedule.WeekOf);
        var started = dispatch.StartAttempt(schedule.WeekOf, schedule.ScheduledAt.AddMinutes(2));

        // Assert
        Assert.True(skipped);
        Assert.Equal("skipped", status.Status);
        Assert.Equal("email_opt_out", status.FailureCode);
        Assert.Equal(0, status.Attempts);
        Assert.False(started);
    }

    [Fact]
    public void ShouldNotSkipInFlightDispatchGivenReservedSmtpAttempt()
    {
        // Arrange
        var schedule = new WorkDigestScheduleWindow(new DateOnly(2026, 10, 5), "UTC",
            new DateTimeOffset(2026, 10, 5, 9, 0, 0, TimeSpan.Zero));
        var dispatch = new WorkDigestDispatch(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        Assert.True(dispatch.Schedule(schedule, Uuid.CreateVersion4(), TimeSpan.FromDays(6),
            schedule.ScheduledAt));
        Assert.True(dispatch.StartAttempt(schedule.WeekOf, schedule.ScheduledAt));

        // Act
        var skipped = dispatch.RecordSkipped(schedule.WeekOf,
            schedule.ScheduledAt.AddMinutes(1), "email_opt_out");
        var inFlight = dispatch.Read(schedule.WeekOf);
        var sent = dispatch.RecordSent(schedule.WeekOf, schedule.ScheduledAt.AddMinutes(2));
        var final = dispatch.Read(schedule.WeekOf);

        // Assert
        Assert.False(skipped);
        Assert.Equal(WorkDigestDispatch.InFlight, inFlight.Status);
        Assert.True(sent);
        Assert.Equal(WorkDigestDispatch.Sent, final.Status);
    }

    [Fact]
    public void ShouldNotStartLateRetryAfterFrozenRetryDeadlineGivenTransientRejection()
    {
        // Arrange
        var schedule = new WorkDigestScheduleWindow(new DateOnly(2026, 10, 5), "UTC",
            new DateTimeOffset(2026, 10, 5, 9, 0, 0, TimeSpan.Zero));
        var deadline = schedule.ScheduledAt.AddHours(6);
        var dispatch = new WorkDigestDispatch(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        Assert.True(dispatch.Schedule(schedule, Uuid.CreateVersion4(), TimeSpan.FromHours(6),
            schedule.ScheduledAt));
        Assert.True(dispatch.StartAttempt(schedule.WeekOf, schedule.ScheduledAt));
        Assert.True(dispatch.RecordTransientRejection(schedule.WeekOf,
            schedule.ScheduledAt.AddMinutes(1), maximumAttempts: 3,
            retryDelay: TimeSpan.FromMinutes(5)));

        // Act
        var lateStart = dispatch.StartAttempt(schedule.WeekOf, deadline);
        var expired = dispatch.RecordRetryWindowExpired(schedule.WeekOf, deadline);

        // Assert
        Assert.False(lateStart);
        Assert.True(expired);
        Assert.Equal(WorkDigestDispatch.RetryExhausted, dispatch.Read(schedule.WeekOf).Status);
        Assert.Equal("retry_window_expired", dispatch.Read(schedule.WeekOf).FailureCode);
    }

    [Fact]
    public void ShouldCapRetryDeadlineAtNextLocalWeekGivenMaximumWindow()
    {
        // Arrange
        var weekOf = new DateOnly(2026, 10, 5);
        var scheduledAt = new DateTimeOffset(2026, 10, 5, 13, 0, 0, TimeSpan.Zero);
        var nextLocalWeek = new DateTimeOffset(2026, 10, 12, 4, 0, 0, TimeSpan.Zero);
        var schedule = new WorkDigestScheduleWindow(weekOf, "America/New_York", scheduledAt);
        var dispatch = new WorkDigestDispatch(Uuid.CreateVersion4(), Uuid.CreateVersion4());

        // Act
        var scheduled = dispatch.Schedule(schedule, Uuid.CreateVersion4(),
            TimeSpan.FromHours(167), scheduledAt);

        // Assert
        Assert.True(scheduled);
        Assert.Equal(nextLocalWeek, dispatch.RetryDeadline(weekOf));
    }

    [Fact]
    public void ShouldCapRetryDeadlineAcrossDaylightSavingGivenMaximumWindow()
    {
        // Arrange
        var weekOf = new DateOnly(2026, 3, 2);
        var scheduledAt = new DateTimeOffset(2026, 3, 2, 14, 0, 0, TimeSpan.Zero);
        var nextLocalWeek = new DateTimeOffset(2026, 3, 9, 4, 0, 0, TimeSpan.Zero);
        var schedule = new WorkDigestScheduleWindow(weekOf, "America/New_York", scheduledAt);
        var dispatch = new WorkDigestDispatch(Uuid.CreateVersion4(), Uuid.CreateVersion4());

        // Act
        var scheduled = dispatch.Schedule(schedule, Uuid.CreateVersion4(),
            TimeSpan.FromHours(167), scheduledAt);

        // Assert
        Assert.True(scheduled);
        Assert.Equal(nextLocalWeek, dispatch.RetryDeadline(weekOf));
    }

    [Fact]
    public void ShouldNotStartAttemptAfterFrozenLocalWeekEndsGivenStoredDispatch()
    {
        // Arrange
        var weekOf = new DateOnly(2026, 10, 5);
        var scheduledAt = new DateTimeOffset(2026, 10, 5, 13, 0, 0, TimeSpan.Zero);
        var nextLocalWeek = new DateTimeOffset(2026, 10, 12, 4, 0, 0, TimeSpan.Zero);
        var dispatch = new WorkDigestDispatch(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        Assert.True(dispatch.Schedule(new WorkDigestScheduleWindow(weekOf,
                "America/New_York", scheduledAt), Uuid.CreateVersion4(), TimeSpan.FromHours(167),
            scheduledAt));

        // Act
        var started = dispatch.StartAttempt(weekOf, nextLocalWeek.AddMinutes(1));

        // Assert
        Assert.False(started);
    }

    [Fact]
    public void ShouldAllowNextUnclaimedWeekGivenOlderDispatch()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var memberId = Uuid.CreateVersion4();
        var dispatch = new WorkDigestDispatch(tenantId, memberId);
        var firstWeek = new WorkDigestScheduleWindow(new DateOnly(2026, 10, 5), "UTC",
            new DateTimeOffset(2026, 10, 5, 9, 0, 0, TimeSpan.Zero));
        var nextWeek = new WorkDigestScheduleWindow(new DateOnly(2026, 10, 12), "UTC",
            new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero));

        // Act
        var firstScheduled = dispatch.Schedule(firstWeek, Uuid.CreateVersion4(),
            TimeSpan.FromHours(167), firstWeek.ScheduledAt);
        var nextScheduled = dispatch.Schedule(nextWeek, Uuid.CreateVersion4(),
            TimeSpan.FromHours(167), nextWeek.ScheduledAt);

        // Assert
        Assert.True(firstScheduled);
        Assert.True(nextScheduled);
    }

    [Fact]
    public void ShouldAuthorizeUnknownRetryOnceGivenCurrentWeekRelayEvidence()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var memberId = Uuid.CreateVersion4();
        var operatorId = Uuid.CreateVersion4();
        var weekOf = new DateOnly(2026, 10, 5);
        var scheduledAt = new DateTimeOffset(2026, 10, 5, 13, 0, 0, TimeSpan.Zero);
        var authorizedAt = scheduledAt.AddHours(2);
        var dispatch = new WorkDigestDispatch(tenantId, memberId);
        Assert.True(dispatch.Schedule(new WorkDigestScheduleWindow(weekOf, "America/New_York",
                scheduledAt), Uuid.CreateVersion4(), TimeSpan.FromHours(167), scheduledAt));
        Assert.True(dispatch.StartAttempt(weekOf, scheduledAt));
        Assert.True(dispatch.RecordUnknown(weekOf, scheduledAt.AddMinutes(1),
            "transport_ambiguous"));

        // Act
        var authorized = dispatch.AuthorizeUnknownRetry(weekOf, operatorId,
            "smtp-record:INC-4201", "Relay logs confirm non-acceptance.", authorizedAt,
            maximumAttempts: 3);
        var status = dispatch.Read(weekOf);
        var duplicateAuthorization = dispatch.AuthorizeUnknownRetry(weekOf, operatorId,
            "smtp-record:INC-4201", "Relay logs confirm non-acceptance.", authorizedAt,
            maximumAttempts: 3);

        // Assert
        Assert.True(authorized);
        Assert.False(duplicateAuthorization);
        Assert.Equal(WorkDigestDispatch.RetryPending, status.Status);
        Assert.Equal(authorizedAt, status.NextAttemptAt);
        Assert.Equal(operatorId, status.RetryAuthorizedBy);
        Assert.Equal(authorizedAt, status.RetryAuthorizedAt);
        Assert.Equal("smtp-record:INC-4201", status.RetryEvidenceReference);
    }

    [Fact]
    public void ShouldRejectUnknownRetryAuthorizationGivenOutsideFrozenWeekOrAttemptLimit()
    {
        // Arrange
        var weekOf = new DateOnly(2026, 10, 5);
        var scheduledAt = new DateTimeOffset(2026, 10, 5, 13, 0, 0, TimeSpan.Zero);
        var operatorId = Uuid.CreateVersion4();
        var dispatch = new WorkDigestDispatch(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        Assert.True(dispatch.Schedule(new WorkDigestScheduleWindow(weekOf, "America/New_York",
                scheduledAt), Uuid.CreateVersion4(), TimeSpan.FromHours(167), scheduledAt));
        Assert.True(dispatch.StartAttempt(weekOf, scheduledAt));
        Assert.True(dispatch.RecordUnknown(weekOf, scheduledAt.AddMinutes(1),
            "transport_ambiguous"));

        // Act
        var outsideWeek = dispatch.AuthorizeUnknownRetry(weekOf, operatorId,
            "smtp-record:INC-4201", "Relay logs confirm non-acceptance.",
            new DateTimeOffset(2026, 10, 12, 4, 0, 0, TimeSpan.Zero), maximumAttempts: 3);

        var exhausted = new WorkDigestDispatch(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        Assert.True(exhausted.Schedule(new WorkDigestScheduleWindow(weekOf, "America/New_York",
                scheduledAt), Uuid.CreateVersion4(), TimeSpan.FromHours(167), scheduledAt));
        Assert.True(exhausted.StartAttempt(weekOf, scheduledAt));
        Assert.True(exhausted.RecordUnknown(weekOf, scheduledAt.AddMinutes(1),
            "transport_ambiguous"));
        var atAttemptLimit = exhausted.AuthorizeUnknownRetry(weekOf, operatorId,
            "smtp-record:INC-4201", "Relay logs confirm non-acceptance.",
            scheduledAt.AddHours(1), maximumAttempts: 1);

        // Assert
        Assert.False(outsideWeek);
        Assert.False(atAttemptLimit);
        Assert.Equal(WorkDigestDispatch.Unknown, dispatch.Read(weekOf).Status);
        Assert.Equal(WorkDigestDispatch.Unknown, exhausted.Read(weekOf).Status);
    }

    [Fact]
    public void ShouldRejectUnknownRetryAuthorizationGivenWrongStatusOrMissingEvidence()
    {
        // Arrange
        var weekOf = new DateOnly(2026, 10, 5);
        var scheduledAt = new DateTimeOffset(2026, 10, 5, 13, 0, 0, TimeSpan.Zero);
        var authorizedAt = scheduledAt.AddHours(1);
        var operatorId = Uuid.CreateVersion4();
        var dispatch = new WorkDigestDispatch(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        Assert.True(dispatch.Schedule(new WorkDigestScheduleWindow(weekOf,
                "America/New_York", scheduledAt), Uuid.CreateVersion4(), TimeSpan.FromHours(167),
            scheduledAt));

        // Act
        var wrongStatus = dispatch.AuthorizeUnknownRetry(weekOf, operatorId,
            "smtp-record:INC-4201", "Relay logs confirm non-acceptance.", authorizedAt,
            maximumAttempts: 3);
        Assert.True(dispatch.StartAttempt(weekOf, scheduledAt));
        Assert.True(dispatch.RecordUnknown(weekOf, scheduledAt.AddMinutes(1),
            "transport_ambiguous"));
        var missingEvidence = dispatch.AuthorizeUnknownRetry(weekOf, operatorId, " ",
            "Relay logs confirm non-acceptance.", authorizedAt, maximumAttempts: 3);

        // Assert
        Assert.False(wrongStatus);
        Assert.False(missingEvidence);
        Assert.Equal(WorkDigestDispatch.Unknown, dispatch.Read(weekOf).Status);
    }

    [Fact]
    public void ShouldSkipScheduledDigestAfterItsLocalWeekWasMissedGivenUnattemptedClaim()
    {
        // Arrange
        var schedule = new WorkDigestScheduleWindow(new DateOnly(2026, 10, 5), "UTC",
            new DateTimeOffset(2026, 10, 5, 9, 0, 0, TimeSpan.Zero));
        var dispatch = new WorkDigestDispatch(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        Assert.True(dispatch.Schedule(schedule, Uuid.CreateVersion4(), TimeSpan.FromDays(6),
            schedule.ScheduledAt));

        // Act
        var missed = dispatch.RecordMissedWeek(schedule.WeekOf,
            schedule.ScheduledAt.AddDays(7));

        // Assert
        Assert.True(missed);
        Assert.Equal(WorkDigestDispatch.Skipped, dispatch.Read(schedule.WeekOf).Status);
        Assert.Equal("missed_week", dispatch.Read(schedule.WeekOf).FailureCode);
    }
}

using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class WorkDigestDispatchProcessorTests
{
    [Fact]
    public async Task ShouldPersistUniqueReservationBeforeSendingAndNeverReplayGivenAcceptedDigest()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var weekOf = new DateOnly(2026, 10, 5);
        var now = new DateTimeOffset(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);
        var clock = new FixedTimeProvider(now);
        var settings = new WorkDigestDeliverySettings("https://app.example",
            "/tenants/{tenant_id}/{tenant_slug}/programs/{program_id}/work/{work_item_id}",
            3, TimeSpan.FromMinutes(5), TimeSpan.FromDays(6), TimeSpan.FromMinutes(3),
            TimeSpan.FromMinutes(1));
        var content = new ReadyDigestContentReader();
        await using var scope = fixture.Provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var delivery = new InspectingDelivery(reader, fixture.TenantId,
            fixture.Member(fixture.OwnerUserId));
        var processor = new WorkDigestDispatchProcessor(reader,
            scope.ServiceProvider.GetRequiredService<IAggregateExecutor>(), content, delivery,
            settings, clock);

        // Act
        await processor.ProcessMemberAsync(fixture.TenantId, fixture.OwnerUserId,
            tenantActive: true, memberActive: true, CancellationToken.None);
        await processor.ProcessMemberAsync(fixture.TenantId, fixture.OwnerUserId,
            tenantActive: true, memberActive: true, CancellationToken.None);
        var persisted = await reader.HydrateAsync(new WorkDigestDispatch(fixture.TenantId,
            fixture.Member(fixture.OwnerUserId)));
        var status = persisted.Read(weekOf);

        // Assert
        Assert.Equal(1, delivery.Calls);
        Assert.Equal(WorkDigestDispatch.InFlight, delivery.StatusAtSend);
        Assert.Equal(WorkDigestDispatch.Sent, status.Status);
        Assert.Equal(1, status.Attempts);
        Assert.Equal(status.MessageId, delivery.MessageId);
        Assert.Equal(1, content.Calls);
    }

    [Fact]
    public async Task ShouldMarkStaleUnattemptedWindowAsMissedGivenExpiredRetryDeadline()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var ownedProvider = fixture.Provider;
        var weekOf = new DateOnly(2026, 10, 5);
        var scheduledAt = new DateTimeOffset(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);
        var now = new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
        var clock = new FixedTimeProvider(now);
        var memberId = fixture.Member(fixture.OwnerUserId);
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new WorkDigestDispatch(fixture.TenantId, memberId), dispatch =>
            {
                Assert.True(dispatch.Schedule(new WorkDigestScheduleWindow(weekOf, "UTC",
                        scheduledAt), Uuid.CreateVersion4(), TimeSpan.FromDays(6), scheduledAt));
                return Result.Success;
            });
        var settings = new WorkDigestDeliverySettings("https://app.example",
            "/tenants/{tenant_id}/{tenant_slug}/programs/{program_id}/work/{work_item_id}",
            3, TimeSpan.FromMinutes(5), TimeSpan.FromDays(6), TimeSpan.FromMinutes(3),
            TimeSpan.FromMinutes(1));
        var content = new ReadyDigestContentReader();
        await using var scope = fixture.Provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var delivery = new InspectingDelivery(reader, fixture.TenantId, memberId);
        var processor = new WorkDigestDispatchProcessor(reader,
            scope.ServiceProvider.GetRequiredService<IAggregateExecutor>(), content, delivery,
            settings, clock);

        // Act
        await processor.ProcessMemberAsync(fixture.TenantId, fixture.OwnerUserId,
            tenantActive: true, memberActive: true, CancellationToken.None);
        var persisted = await reader.HydrateAsync(new WorkDigestDispatch(fixture.TenantId, memberId));
        var status = persisted.Read(weekOf);

        // Assert
        Assert.Equal(WorkDigestDispatch.Skipped, status.Status);
        Assert.Equal("missed_week", status.FailureCode);
        Assert.Equal(0, status.Attempts);
        Assert.Equal(0, content.Calls);
        Assert.Equal(0, delivery.Calls);
    }

    [Fact]
    public async Task ShouldNotBackfillEarlierWeekGivenTimeZoneChangeAcrossWeekBoundary()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var ownedProvider = fixture.Provider;
        var now = new DateTimeOffset(2026, 10, 11, 19, 5, 0, TimeSpan.Zero);
        var clock = new FixedTimeProvider(now);
        var memberId = fixture.Member(fixture.OwnerUserId);
        var actor = ActorReference.ForMember(memberId, "member");
        var currentWindowDue = WorkDigestSchedule.TryGetDue(now, "Pacific/Kiritimati",
            out var currentWindow);
        var changedZoneWindowDue = WorkDigestSchedule.TryGetDue(now, "Pacific/Honolulu",
            out var changedZoneWindow);
        Assert.True(currentWindowDue);
        Assert.Equal(new DateOnly(2026, 10, 12), currentWindow.WeekOf);
        Assert.True(changedZoneWindowDue);
        Assert.Equal(new DateOnly(2026, 10, 5), changedZoneWindow.WeekOf);
        var settings = new WorkDigestDeliverySettings("https://app.example",
            "/tenants/{tenant_id}/{tenant_slug}/programs/{program_id}/work/{work_item_id}",
            3, TimeSpan.FromMinutes(5), TimeSpan.FromHours(167), TimeSpan.FromMinutes(3),
            TimeSpan.FromMinutes(1));
        Assert.True(now < changedZoneWindow.ScheduledAt + settings.RetryWindow);
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new WorkDigestPreference(fixture.TenantId, memberId), preference =>
            {
                preference.Set(true, "Pacific/Kiritimati", actor, now.AddMinutes(-1));
                return Result.Success;
            });
        await using var scope = fixture.Provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var delivery = new CountingDelivery();
        var processor = new WorkDigestDispatchProcessor(reader,
            scope.ServiceProvider.GetRequiredService<IAggregateExecutor>(),
            new ReadyDigestContentReader(), delivery, settings, clock);

        // Act
        await processor.ProcessMemberAsync(fixture.TenantId, fixture.OwnerUserId,
            tenantActive: true, memberActive: true, CancellationToken.None);
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new WorkDigestPreference(fixture.TenantId, memberId), preference =>
            {
                preference.Set(true, "Pacific/Honolulu", actor, now);
                return Result.Success;
            });
        await processor.ProcessMemberAsync(fixture.TenantId, fixture.OwnerUserId,
            tenantActive: true, memberActive: true, CancellationToken.None);
        var persisted = await reader.HydrateAsync(new WorkDigestDispatch(fixture.TenantId,
            memberId));

        // Assert
        Assert.Equal(WorkDigestDispatch.Sent, persisted.Read(currentWindow.WeekOf).Status);
        Assert.Equal("not_scheduled", persisted.Read(changedZoneWindow.WeekOf).Status);
        Assert.Equal(1, delivery.Calls);
    }

    [Fact]
    public async Task ShouldNotSendRetryAfterFrozenLocalWeekEndsGivenDueRetry()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var ownedProvider = fixture.Provider;
        var weekOf = new DateOnly(2026, 10, 5);
        var scheduledAt = new DateTimeOffset(2026, 10, 5, 13, 0, 0, TimeSpan.Zero);
        var retryDueAt = new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
        var memberId = fixture.Member(fixture.OwnerUserId);
        var clock = new FixedTimeProvider(retryDueAt);
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new WorkDigestDispatch(fixture.TenantId, memberId), dispatch =>
            {
                Assert.True(dispatch.Schedule(new WorkDigestScheduleWindow(weekOf,
                        "America/New_York", scheduledAt), Uuid.CreateVersion4(),
                    TimeSpan.FromHours(167), scheduledAt));
                Assert.True(dispatch.StartAttempt(weekOf, scheduledAt));
                return dispatch.RecordTransientRejection(weekOf,
                    new DateTimeOffset(2026, 10, 11, 13, 0, 0, TimeSpan.Zero),
                    maximumAttempts: 3, retryDelay: TimeSpan.FromHours(20))
                    ? Result.Success
                    : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The test digest retry could not be seeded."));
            });
        var settings = new WorkDigestDeliverySettings("https://app.example",
            "/tenants/{tenant_id}/{tenant_slug}/programs/{program_id}/work/{work_item_id}",
            3, TimeSpan.FromHours(20), TimeSpan.FromHours(167), TimeSpan.FromMinutes(3),
            TimeSpan.FromMinutes(1));
        var content = new ReadyDigestContentReader();
        await using var scope = fixture.Provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var delivery = new CountingDelivery();
        var processor = new WorkDigestDispatchProcessor(reader,
            scope.ServiceProvider.GetRequiredService<IAggregateExecutor>(), content, delivery,
            settings, clock);

        // Act
        await processor.ProcessMemberAsync(fixture.TenantId, fixture.OwnerUserId,
            tenantActive: true, memberActive: false, CancellationToken.None);
        var persisted = await reader.HydrateAsync(new WorkDigestDispatch(fixture.TenantId,
            memberId));
        var status = persisted.Read(weekOf);

        // Assert
        Assert.Equal(WorkDigestDispatch.RetryExhausted, status.Status);
        Assert.Equal(0, content.Calls);
        Assert.Equal(0, delivery.Calls);
    }

    sealed class ReadyDigestContentReader : IWorkDigestContentReader
    {
        public int Calls { get; private set; }

        public ValueTask<WorkDigestContentRead> ReadAsync(Uuid tenantId, Uuid userId,
            WorkDigestDispatchStatusView dispatch, CancellationToken ct)
        {
            Calls++;
            return ValueTask.FromResult(WorkDigestContentRead.Ready(new WorkDigestDeliveryMessage(
                dispatch.MessageId, "member@example.com", "Weekly digest", "One assigned item.")));
        }
    }

    sealed class CountingDelivery : IWorkDigestDelivery
    {
        public int Calls { get; private set; }

        public ValueTask<WorkDigestTransportOutcome> SendAsync(
            WorkDigestDeliveryMessage message, CancellationToken ct)
        {
            Calls++;
            return ValueTask.FromResult(new WorkDigestTransportOutcome(
                WorkDigestTransportOutcomeKind.Accepted));
        }
    }

    sealed class InspectingDelivery(IAggregateReader reader, Uuid tenantId, Uuid memberId)
        : IWorkDigestDelivery
    {
        public int Calls { get; private set; }
        public string? StatusAtSend { get; private set; }
        public Uuid MessageId { get; private set; }

        public async ValueTask<WorkDigestTransportOutcome> SendAsync(
            WorkDigestDeliveryMessage message, CancellationToken ct)
        {
            Calls++;
            MessageId = message.MessageId;
            var aggregate = await reader.HydrateAsync(new WorkDigestDispatch(tenantId, memberId), ct);
            StatusAtSend = aggregate.Read(new DateOnly(2026, 10, 5)).Status;
            return new WorkDigestTransportOutcome(WorkDigestTransportOutcomeKind.Accepted);
        }
    }

    sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

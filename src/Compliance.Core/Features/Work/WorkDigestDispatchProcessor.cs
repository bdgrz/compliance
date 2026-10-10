using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Claims, sends, and records one member's durable weekly digest dispatches.</summary>
sealed class WorkDigestDispatchProcessor(IAggregateReader reader, IAggregateExecutor executor,
    IWorkDigestContentReader contentReader, IWorkDigestDelivery delivery,
    WorkDigestDeliverySettings settings, TimeProvider clock)
{
    public async ValueTask ProcessMemberAsync(Uuid tenantId, Uuid userId,
        bool tenantActive, bool memberActive, CancellationToken ct)
    {
        if (!settings.IsReady)
            return;

        var memberId = RbacIds.Member(tenantId, userId);
        var preferenceAggregate = await reader.HydrateAsync(
            new WorkDigestPreference(tenantId, memberId), ct).ConfigureAwait(false);
        var preference = preferenceAggregate.Read();
        var dispatch = await reader.HydrateAsync(new WorkDigestDispatch(tenantId, memberId), ct)
            .ConfigureAwait(false);
        var pending = dispatch.FindPending();
        if (pending is not null)
        {
            await ProcessPendingAsync(tenantId, userId, pending, dispatch, ct)
                .ConfigureAwait(false);
            return;
        }

        if (!tenantActive || !memberActive ||
            !WorkDigestSchedule.TryGetDue(clock.GetUtcNow(), preference.TimeZoneId,
                out var window) || dispatch.Read(window.WeekOf).Status != "not_scheduled")
            return;

        var messageId = Uuid.CreateVersion5(tenantId,
            string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"work-digest:{memberId}:{window.WeekOf:yyyy-MM-dd}"));
        var now = clock.GetUtcNow();
        if (!await MutateAsync(tenantId, memberId,
                aggregate => aggregate.Schedule(window, messageId, settings.RetryWindow, now), ct)
            .ConfigureAwait(false))
            return;

        var scheduled = await reader.HydrateAsync(new WorkDigestDispatch(tenantId, memberId), ct)
            .ConfigureAwait(false);
        var current = scheduled.Read(window.WeekOf);
        await ProcessPendingAsync(tenantId, userId, current, scheduled, ct).ConfigureAwait(false);
    }

    async ValueTask ProcessPendingAsync(Uuid tenantId, Uuid userId,
        WorkDigestDispatchStatusView pending, WorkDigestDispatch dispatch,
        CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        switch (pending.Status)
        {
            case WorkDigestDispatch.InFlight:
                if (pending.LastAttemptAt is { } attemptAt && now >= attemptAt + settings.AttemptLease)
                    await MutateAsync(tenantId, pending.MemberId,
                        aggregate => aggregate.RecordInterruptedAttempt(pending.WeekOf, now), ct)
                        .ConfigureAwait(false);
                return;
            case WorkDigestDispatch.Scheduled:
                if (!WorkDigestSchedule.IsCurrentWeek(pending.WeekOf, pending.TimeZoneId, now))
                {
                    await MutateAsync(tenantId, pending.MemberId,
                        aggregate => aggregate.RecordMissedWeek(pending.WeekOf, now), ct)
                        .ConfigureAwait(false);
                    return;
                }
                if (now >= dispatch.RetryDeadline(pending.WeekOf))
                {
                    await ExpireAsync(tenantId, pending, now, ct).ConfigureAwait(false);
                    return;
                }
                if (now < pending.ScheduledAt)
                    return;
                break;
            case WorkDigestDispatch.RetryPending:
                if (now >= dispatch.RetryDeadline(pending.WeekOf))
                {
                    await ExpireAsync(tenantId, pending, now, ct).ConfigureAwait(false);
                    return;
                }
                if (pending.NextAttemptAt is not { } nextAttempt || now < nextAttempt)
                    return;
                break;
            default:
                return;
        }

        var content = await contentReader.ReadAsync(tenantId, userId, pending, ct)
            .ConfigureAwait(false);
        if (content.Kind == WorkDigestContentReadKind.Unavailable)
            return;
        if (content.Kind == WorkDigestContentReadKind.Skip)
        {
            await MutateAsync(tenantId, pending.MemberId,
                aggregate => aggregate.RecordSkipped(pending.WeekOf, clock.GetUtcNow(),
                    content.SkipReason ?? "recipient_unavailable"), ct).ConfigureAwait(false);
            return;
        }

        var message = content.Message ?? throw new InvalidOperationException(
            "A ready digest must contain a message.");
        var startedAt = clock.GetUtcNow();
        if (!await MutateAsync(tenantId, pending.MemberId,
                aggregate => aggregate.StartAttempt(pending.WeekOf, startedAt), ct)
            .ConfigureAwait(false))
            return;

        WorkDigestTransportOutcome outcome;
        try
        {
            outcome = await delivery.SendAsync(message, ct).ConfigureAwait(false);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // A transport exception without a definite SMTP rejection may follow relay acceptance.
            outcome = new WorkDigestTransportOutcome(WorkDigestTransportOutcomeKind.Unknown,
                "transport_ambiguous");
        }

        var recordedAt = clock.GetUtcNow();
        await MutateAsync(tenantId, pending.MemberId, aggregate => outcome.Kind switch
        {
            WorkDigestTransportOutcomeKind.Accepted =>
                aggregate.RecordSent(pending.WeekOf, recordedAt),
            WorkDigestTransportOutcomeKind.DefiniteTransientRejection =>
                aggregate.RecordTransientRejection(pending.WeekOf, recordedAt,
                    settings.MaximumAttempts, settings.RetryDelay),
            WorkDigestTransportOutcomeKind.DefinitePermanentRejection =>
                aggregate.RecordPermanentRejection(pending.WeekOf, recordedAt,
                    outcome.FailureCode ?? "smtp_permanent_rejection"),
            _ => aggregate.RecordUnknown(pending.WeekOf, recordedAt,
                outcome.FailureCode ?? "transport_ambiguous"),
        }, ct).ConfigureAwait(false);
    }

    async ValueTask ExpireAsync(Uuid tenantId, WorkDigestDispatchStatusView pending,
        DateTimeOffset now, CancellationToken ct)
    {
        _ = await MutateAsync(tenantId, pending.MemberId,
            aggregate => aggregate.RecordRetryWindowExpired(pending.WeekOf, now), ct)
            .ConfigureAwait(false);
    }

    async ValueTask<bool> MutateAsync(Uuid tenantId, Uuid memberId,
        Func<WorkDigestDispatch, bool> mutation, CancellationToken ct)
    {
        var request = new WorkDigestDispatchMutation(tenantId, memberId);
        try
        {
            var result = await executor.ExecuteAsync(new WorkDigestDispatch(tenantId, memberId),
                aggregate =>
                {
                    var changed = mutation(aggregate);
                    var value = Result<bool>.Success(changed);
                    return changed ? AggregateOutcome.Commit(value) : AggregateOutcome.Discard(value);
                }, new RequestContext<WorkDigestDispatchMutation>(request, RequestActor.System), ct)
                .ConfigureAwait(false);
            return result.IsSuccess && result.Value;
        }
        catch (EventStreamConcurrencyException)
        {
            // Another host has the durable claim. This invocation must not send.
            return false;
        }
    }

    sealed record WorkDigestDispatchMutation(Uuid TenantId, Uuid MemberId) : IRequest;
}

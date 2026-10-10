using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Durable member-level ledger for weekly digest reservations and delivery outcomes.</summary>
public sealed class WorkDigestDispatch : Aggregate
{
    public const string Scheduled = "scheduled";
    public const string InFlight = "in_flight";
    public const string RetryPending = "retry_pending";
    public const string Sent = "sent";
    public const string Skipped = "skipped";
    public const string PermanentFailure = "permanent_failure";
    public const string RetryExhausted = "retry_exhausted";
    public const string Unknown = "unknown";

    readonly Uuid _tenantId;
    readonly Dictionary<DateOnly, DispatchState> _dispatches = [];

    public WorkDigestDispatch(Uuid tenantId, Uuid memberId)
        : base(memberId, new EventStreamAddress(tenantId.ToString(), "work-digest-dispatches",
            memberId.ToString()))
    {
        _tenantId = tenantId;
        On<WorkDigestDispatchScheduled>(Apply);
        On<WorkDigestDispatchAttemptStarted>(Apply);
        On<WorkDigestDispatchOutcomeRecorded>(Apply);
        On<WorkDigestUnknownRetryAuthorized>(Apply);
    }

    internal bool Schedule(WorkDigestScheduleWindow window, Uuid messageId,
        TimeSpan retryWindow, DateTimeOffset recordedAt)
    {
        if (_dispatches.ContainsKey(window.WeekOf) ||
            _dispatches.Keys.Any(existingWeek => existingWeek > window.WeekOf) ||
            retryWindow <= TimeSpan.Zero)
            return false;
        var retryDeadline = window.ScheduledAt + retryWindow;
        var nextWeek = WorkDigestSchedule.NextWeekStartUtc(window.WeekOf, window.TimeZoneId);
        if (nextWeek < retryDeadline)
            retryDeadline = nextWeek;
        RaiseEvent(new WorkDigestDispatchScheduled(_tenantId, Id, window.WeekOf,
            WorkDigestSchedule.NormalizeTimeZoneId(window.TimeZoneId), window.ScheduledAt,
            messageId, retryDeadline, recordedAt));
        return true;
    }

    public WorkDigestDispatchStatusView Read(DateOnly weekOf) =>
        _dispatches.TryGetValue(weekOf, out var state)
            ? state.View(_tenantId, Id)
            : new WorkDigestDispatchStatusView(_tenantId, Id, weekOf, "UTC",
                DateTimeOffset.MinValue, "not_scheduled", 0, null, Uuid.Empty,
                DateTimeOffset.MinValue, null, null, null, null, null);

    public WorkDigestDispatchStatusView? FindCurrentWindow(DateTimeOffset now) =>
        _dispatches.Values.OrderByDescending(static item => item.ScheduledAt)
            .FirstOrDefault(state => WorkDigestSchedule.IsCurrentWeek(state.WeekOf,
                state.TimeZoneId, now))?.View(_tenantId, Id);

    public WorkDigestDispatchStatusView? FindPending() =>
        _dispatches.Values.Where(static state => state.Status is
                Scheduled or InFlight or RetryPending)
            .OrderBy(static state => state.ScheduledAt)
            .FirstOrDefault()?.View(_tenantId, Id);

    internal DateTimeOffset RetryDeadline(DateOnly weekOf) =>
        _dispatches.TryGetValue(weekOf, out var state)
            ? state.RetryDeadline
            : DateTimeOffset.MinValue;

    public bool StartAttempt(DateOnly weekOf, DateTimeOffset startedAt)
    {
        if (!_dispatches.TryGetValue(weekOf, out var state) || startedAt < state.ScheduledAt ||
            startedAt >= state.RetryDeadline ||
            !WorkDigestSchedule.IsCurrentWeek(state.WeekOf, state.TimeZoneId, startedAt) ||
            state.Status != Scheduled &&
            (state.Status != RetryPending || state.NextAttemptAt is not { } next || startedAt < next))
            return false;
        RaiseEvent(new WorkDigestDispatchAttemptStarted(_tenantId, Id, weekOf,
            state.Attempts + 1, startedAt));
        return true;
    }

    public bool RecordSent(DateOnly weekOf, DateTimeOffset recordedAt) =>
        Record(weekOf, Sent, recordedAt, null, null);

    public bool RecordSkipped(DateOnly weekOf, DateTimeOffset recordedAt, string reason) =>
        _dispatches.TryGetValue(weekOf, out var state) &&
        state.Status is (Scheduled or RetryPending) &&
        RecordOutcome(weekOf, Skipped, recordedAt, null, reason);

    public bool RecordPermanentRejection(DateOnly weekOf, DateTimeOffset recordedAt,
        string failureCode) => Record(weekOf, PermanentFailure, recordedAt, null, failureCode);

    public bool RecordUnknown(DateOnly weekOf, DateTimeOffset recordedAt, string failureCode) =>
        Record(weekOf, Unknown, recordedAt, null, failureCode);

    public bool RecordInterruptedAttempt(DateOnly weekOf, DateTimeOffset recordedAt) =>
        _dispatches.TryGetValue(weekOf, out var state) && state.Status == InFlight &&
        Record(weekOf, Unknown, recordedAt, null, "worker_interrupted");

    public bool AuthorizeUnknownRetry(DateOnly weekOf, Uuid authorizedBy,
        string evidenceReference, string rationale, DateTimeOffset authorizedAt,
        int maximumAttempts)
    {
        if (!_dispatches.TryGetValue(weekOf, out var state) || state.Status != Unknown ||
            authorizedBy == Uuid.Empty || state.Attempts >= maximumAttempts ||
            authorizedAt < state.ScheduledAt || authorizedAt >= state.RetryDeadline ||
            !WorkDigestSchedule.IsCurrentWeek(state.WeekOf, state.TimeZoneId, authorizedAt) ||
            !IsValidAuditValue(evidenceReference, 256) || !IsValidAuditValue(rationale, 512))
            return false;

        RaiseEvent(new WorkDigestUnknownRetryAuthorized(_tenantId, Id, weekOf,
            state.MessageId, state.Attempts, authorizedBy, evidenceReference.Trim(),
            rationale.Trim(), authorizedAt));
        return true;
    }

    public bool RecordTransientRejection(DateOnly weekOf, DateTimeOffset recordedAt,
        int maximumAttempts, TimeSpan retryDelay)
    {
        if (!_dispatches.TryGetValue(weekOf, out var state) || state.Status != InFlight)
            return false;
        if (state.Attempts >= maximumAttempts || recordedAt + retryDelay >= state.RetryDeadline)
            return Record(weekOf, RetryExhausted, recordedAt, null, "transient_rejection");
        return Record(weekOf, RetryPending, recordedAt, recordedAt + retryDelay,
            "transient_rejection");
    }

    public bool RecordRetryWindowExpired(DateOnly weekOf, DateTimeOffset recordedAt)
    {
        if (!_dispatches.TryGetValue(weekOf, out var state) ||
            state.Status is not (Scheduled or RetryPending))
            return false;
        var expired = recordedAt >= state.RetryDeadline;
        var localWeekEnded = !WorkDigestSchedule.IsCurrentWeek(state.WeekOf,
            state.TimeZoneId, recordedAt);
        if (!expired && !localWeekEnded)
            return false;
        return RecordOutcome(weekOf, RetryExhausted, recordedAt, null,
            expired ? "retry_window_expired" : "local_week_ended");
    }

    public bool RecordMissedWeek(DateOnly weekOf, DateTimeOffset recordedAt) =>
        _dispatches.TryGetValue(weekOf, out var state) && state.Status == Scheduled &&
        !WorkDigestSchedule.IsCurrentWeek(state.WeekOf, state.TimeZoneId, recordedAt) &&
        RecordOutcome(weekOf, Skipped, recordedAt, null, "missed_week");

    bool Record(DateOnly weekOf, string status, DateTimeOffset recordedAt,
        DateTimeOffset? nextAttemptAt, string? failureCode)
    {
        if (!_dispatches.TryGetValue(weekOf, out var state) || state.Status != InFlight)
            return false;
        return RecordOutcome(weekOf, status, recordedAt, nextAttemptAt, failureCode);
    }

    bool RecordOutcome(DateOnly weekOf, string status, DateTimeOffset recordedAt,
        DateTimeOffset? nextAttemptAt, string? failureCode)
    {
        RaiseEvent(new WorkDigestDispatchOutcomeRecorded(_tenantId, Id, weekOf, status,
            recordedAt, nextAttemptAt, failureCode));
        return true;
    }

    static bool IsValidAuditValue(string? value, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maxLength &&
        !value.Any(char.IsControl);

    void Apply(WorkDigestDispatchScheduled scheduled)
    {
        _dispatches.Add(scheduled.WeekOf, new DispatchState(scheduled.WeekOf,
            scheduled.TimeZoneId, scheduled.ScheduledAt, scheduled.RetryDeadline,
            scheduled.MessageId, Scheduled,
            0, null, scheduled.RecordedAt, null, null));
    }

    void Apply(WorkDigestDispatchAttemptStarted started)
    {
        var state = _dispatches[started.WeekOf];
        _dispatches[started.WeekOf] = state with
        {
            Status = InFlight,
            Attempts = started.Attempt,
            LastAttemptAt = started.StartedAt,
            LastUpdatedAt = started.StartedAt,
            NextAttemptAt = null,
        };
    }

    void Apply(WorkDigestDispatchOutcomeRecorded recorded)
    {
        var state = _dispatches[recorded.WeekOf];
        _dispatches[recorded.WeekOf] = state with
        {
            Status = recorded.Status,
            LastUpdatedAt = recorded.RecordedAt,
            NextAttemptAt = recorded.NextAttemptAt,
            FailureCode = recorded.FailureCode,
        };
    }

    void Apply(WorkDigestUnknownRetryAuthorized authorized)
    {
        var state = _dispatches[authorized.WeekOf];
        _dispatches[authorized.WeekOf] = state with
        {
            Status = RetryPending,
            LastUpdatedAt = authorized.AuthorizedAt,
            NextAttemptAt = authorized.AuthorizedAt,
            FailureCode = "operator_authorized_retry",
            RetryAuthorizedBy = authorized.AuthorizedBy,
            RetryAuthorizedAt = authorized.AuthorizedAt,
            RetryEvidenceReference = authorized.EvidenceReference,
        };
    }

    sealed record DispatchState(DateOnly WeekOf, string TimeZoneId, DateTimeOffset ScheduledAt,
        DateTimeOffset RetryDeadline, Uuid MessageId, string Status, int Attempts,
        DateTimeOffset? LastAttemptAt,
        DateTimeOffset LastUpdatedAt, DateTimeOffset? NextAttemptAt, string? FailureCode,
        Uuid? RetryAuthorizedBy = null, DateTimeOffset? RetryAuthorizedAt = null,
        string? RetryEvidenceReference = null)
    {
        public WorkDigestDispatchStatusView View(Uuid tenantId, Uuid memberId) =>
            new(tenantId, memberId, WeekOf, TimeZoneId, ScheduledAt, Status, Attempts,
                LastAttemptAt, MessageId, LastUpdatedAt, NextAttemptAt, FailureCode,
                RetryAuthorizedBy, RetryAuthorizedAt, RetryEvidenceReference);
    }
}

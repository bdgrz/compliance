using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>
///     One member's weekly email digest preference. Members are subscribed until they opt out; the
///     preference never affects in-app reminders.
/// </summary>
public sealed class WorkDigestPreference : Aggregate
{
    readonly Uuid _tenantId;
    bool _emailDigestEnabled = true;
    DateTimeOffset? _changedAt;
    string _timeZoneId = "UTC";

    public WorkDigestPreference(Uuid tenantId, Uuid memberId)
        : base(memberId, new EventStreamAddress(tenantId.ToString(), "work-digest-preferences",
            memberId.ToString()))
    {
        _tenantId = tenantId;
        On<WorkDigestPreferenceChanged>(ev =>
        {
            _emailDigestEnabled = ev.EmailDigestEnabled;
            _changedAt = ev.ChangedAt;
            _timeZoneId = WorkDigestSchedule.NormalizeTimeZoneId(ev.TimeZoneId);
        });
    }

    public WorkDigestPreferenceView Read() => new(_emailDigestEnabled, _changedAt)
    {
        TimeZoneId = _timeZoneId,
    };

    /// <summary>Records a change; setting the current value again records nothing.</summary>
    public void Set(bool emailDigestEnabled, ActorReference actor, DateTimeOffset at)
        => Set(emailDigestEnabled, null, actor, at);

    /// <summary>Records the email preference and optional member-local scheduling zone.</summary>
    public void Set(bool emailDigestEnabled, string? timeZoneId, ActorReference actor,
        DateTimeOffset at)
    {
        var resolvedTimeZoneId = timeZoneId is null
            ? _timeZoneId
            : WorkDigestSchedule.NormalizeTimeZoneId(timeZoneId);
        if (timeZoneId is not null && !WorkDigestSchedule.IsValidTimeZone(timeZoneId))
            throw new ArgumentException("The time zone is invalid.", nameof(timeZoneId));
        if (emailDigestEnabled == _emailDigestEnabled && resolvedTimeZoneId == _timeZoneId)
            return;
        RaiseEvent(new WorkDigestPreferenceChanged(_tenantId, Id, emailDigestEnabled, actor, at)
        {
            TimeZoneId = resolvedTimeZoneId,
        });
    }
}

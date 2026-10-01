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

    public WorkDigestPreference(Uuid tenantId, Uuid memberId)
        : base(memberId, new EventStreamAddress(tenantId.ToString(), "work-digest-preferences",
            memberId.ToString()))
    {
        _tenantId = tenantId;
        On<WorkDigestPreferenceChanged>(ev =>
        {
            _emailDigestEnabled = ev.EmailDigestEnabled;
            _changedAt = ev.ChangedAt;
        });
    }

    public WorkDigestPreferenceView Read() => new(_emailDigestEnabled, _changedAt);

    /// <summary>Records a change; setting the current value again records nothing.</summary>
    public void Set(bool emailDigestEnabled, ActorReference actor, DateTimeOffset at)
    {
        if (emailDigestEnabled == _emailDigestEnabled)
            return;
        RaiseEvent(new WorkDigestPreferenceChanged(_tenantId, Id, emailDigestEnabled, actor, at));
    }
}

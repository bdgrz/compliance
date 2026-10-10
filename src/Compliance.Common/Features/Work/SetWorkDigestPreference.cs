using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Opts the acting member in to or out of the weekly email digest; in-app reminders always remain.</summary>
[Discriminator("bdgrz.work.digest_preference.set", 1)]
public sealed record SetWorkDigestPreference(Uuid TenantId, bool EmailDigestEnabled,
    string? TimeZoneId = null)
    : IRequest<WorkDigestPreferenceView>, ITenantAccessRequest, ICallable;

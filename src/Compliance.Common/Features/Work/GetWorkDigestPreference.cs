using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Reads the acting member's weekly email digest preference.</summary>
[Discriminator("bdgrz.work.digest_preference.get", 1)]
public sealed record GetWorkDigestPreference(Uuid TenantId)
    : IRequest<WorkDigestPreferenceView>, ITenantAccessRequest, ICallable;

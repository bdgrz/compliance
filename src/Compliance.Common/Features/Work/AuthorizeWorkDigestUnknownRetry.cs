using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Authorizes a same-week retry after relay evidence confirms non-acceptance.</summary>
[Discriminator("bdgrz.work.digest.dispatch.unknown_retry.authorize", 1)]
public sealed record AuthorizeWorkDigestUnknownRetry(Uuid TenantId, Uuid MemberId,
    DateOnly WeekOf, string EvidenceReference, string Rationale)
    : IRequest<WorkDigestDispatchStatusView>, ICallable, IPlatformOperatorRequest;

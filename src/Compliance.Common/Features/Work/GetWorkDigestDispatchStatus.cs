using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Reads a member's durable weekly digest delivery status for one tenant-local record.</summary>
[Discriminator("bdgrz.work.digest.dispatch.status.get", 1)]
public sealed record GetWorkDigestDispatchStatus(Uuid TenantId, Uuid MemberId, DateOnly WeekOf)
    : IRequest<WorkDigestDispatchStatusView>, ICallable, IPlatformOperatorRequest;

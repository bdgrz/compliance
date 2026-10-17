using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Records the deliver weekly work digest intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.work.deliver-weekly-work-digest", 1)]
public sealed record DeliverWeeklyWorkDigest(Uuid TenantId, Uuid MemberId, Uuid DigestId, string DeliveryKey) : IRequest, IClientManagementMutationRequest;

using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Records the record work digest delivery intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.work.record-work-digest-delivery", 1)]
public sealed record RecordWorkDigestDelivery(Uuid TenantId, Uuid DigestId, string Outcome, DateTimeOffset AttemptedAt, string? FailureCode) : IRequest, IClientManagementMutationRequest;

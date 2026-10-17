using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AttestRecords;

/// <summary>Records the record own attest record delivery intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.attest-records.record-own-attest-record-delivery", 1)]
public sealed record RecordOwnAttestRecordDelivery(Uuid TenantId, Uuid EngagementId, Uuid PackageId, string Recipient, DateTimeOffset DeliveredAt, string DeliveryReference) : IRequest, IClientManagementMutationRequest, ICallable;

using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Exports;

/// <summary>Records the record client export delivery intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.exports.record-client-export-delivery", 1)]
public sealed record RecordClientExportDelivery(Uuid TenantId, Uuid ExportId, string Recipient, DateTimeOffset DeliveredAt, string DeliveryReference) : IRequest, IClientManagementMutationRequest, ICallable;

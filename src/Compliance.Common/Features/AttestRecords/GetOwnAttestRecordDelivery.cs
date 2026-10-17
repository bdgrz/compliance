using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AttestRecords;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.attest-records.get-own-attest-record-delivery", 1)]
public sealed record GetOwnAttestRecordDelivery(Uuid TenantId, Uuid ProgramId, Uuid OwnAttestRecordDeliveryId)
    : IRequest<OwnAttestRecordDeliveryView>, IProgramReadRequest, ICallable;

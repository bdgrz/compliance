using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;
using Cntryl.Fitz.Extensions;

namespace Bdgrz.Compliance.Features.AttestRecords;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.attest-records.list-outstanding-attest-delivery-holds", 1)]
public sealed record ListOutstandingAttestDeliveryHolds(Uuid TenantId, Uuid ProgramId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<AttestDeliveryHoldView>>, IProgramReadRequest, ICallable;

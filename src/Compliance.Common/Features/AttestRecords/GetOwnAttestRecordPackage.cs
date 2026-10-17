using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AttestRecords;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.attest-records.get-own-attest-record-package", 1)]
public sealed record GetOwnAttestRecordPackage(Uuid TenantId, Uuid ProgramId, Uuid OwnAttestRecordPackageId)
    : IRequest<OwnAttestRecordPackageView>, IProgramReadRequest, ICallable;

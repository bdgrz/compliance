using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AttestRecords;

/// <summary>Records the request own attest record package intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.attest-records.request-own-attest-record-package", 1)]
public sealed record RequestOwnAttestRecordPackage(Uuid TenantId, Uuid EngagementId, Uuid PackageId, string PackageKey) : IRequest, IClientManagementMutationRequest, ICallable;

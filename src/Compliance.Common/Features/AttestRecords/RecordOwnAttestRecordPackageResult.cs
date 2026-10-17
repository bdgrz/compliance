using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AttestRecords;

/// <summary>Records the record own attest record package result intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.attest-records.record-own-attest-record-package-result", 1)]
public sealed record RecordOwnAttestRecordPackageResult(Uuid TenantId, Uuid PackageId, string Outcome, Uuid? ArtifactId, string? FailureCode, DateTimeOffset CompletedAt) : IRequest, IClientManagementMutationRequest;

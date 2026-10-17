using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Records the record audit package build result intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.audit-packages.record-audit-package-build-result", 1)]
public sealed record RecordAuditPackageBuildResult(Uuid TenantId, Uuid ProgramId, Uuid PackageId, string Outcome, Uuid? ArtifactId, string? FailureCode, DateTimeOffset CompletedAt) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest;

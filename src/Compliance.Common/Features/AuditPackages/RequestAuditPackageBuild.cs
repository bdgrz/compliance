using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Records the request audit package build intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.audit-packages.request-audit-package-build", 1)]
public sealed record RequestAuditPackageBuild(Uuid TenantId, Uuid ProgramId, Uuid ExaminationId, Uuid PackageId, string PackageKind, string BuildKey) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;

using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Records the validate audit package intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.audit-packages.validate-audit-package", 1)]
public sealed record ValidateAuditPackage(Uuid TenantId, Uuid ProgramId, Uuid PackageId, long PackageVersion, string ValidationKey) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;

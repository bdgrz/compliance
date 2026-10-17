using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Records the approve audit package disclosure intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.audit-packages.approve-audit-package-disclosure", 1)]
public sealed record ApproveAuditPackageDisclosure(Uuid TenantId, Uuid ProgramId, Uuid PackageId, long PackageVersion, string Recipient, string Purpose, string Rationale) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;

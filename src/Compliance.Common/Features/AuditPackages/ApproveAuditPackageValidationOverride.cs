using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>Records the approve audit package validation override intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.audit-packages.approve-audit-package-validation-override", 1)]
public sealed record ApproveAuditPackageValidationOverride(Uuid TenantId, Uuid ProgramId, Uuid PackageId, long PackageVersion, string ValidationIssue, string Rationale) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;

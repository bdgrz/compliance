using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>Records the complete tenant offboarding intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.tenants.complete-tenant-offboarding", 1)]
public sealed record CompleteTenantOffboarding(Uuid TenantId, Uuid OffboardingId, long ExpectedRevision, string ExportReference, string RetentionDecisionReference, string Rationale) : IRequest, IClientManagementMutationRequest, ICallable;

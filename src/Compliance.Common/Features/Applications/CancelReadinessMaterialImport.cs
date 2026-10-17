using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Records the cancel readiness material import intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.imports.cancel-readiness-material-import", 1)]
public sealed record CancelReadinessMaterialImport(Uuid TenantId, Uuid ImportId, string Rationale) : IRequest, IClientManagementMutationRequest, ICallable;

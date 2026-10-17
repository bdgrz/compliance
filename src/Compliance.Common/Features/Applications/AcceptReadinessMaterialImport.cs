using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Records the accept readiness material import intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.imports.accept-readiness-material-import", 1)]
public sealed record AcceptReadinessMaterialImport(Uuid TenantId, Uuid ImportId, long ExpectedRevision, string AcceptanceRationale) : IRequest, IClientManagementMutationRequest, ICallable;

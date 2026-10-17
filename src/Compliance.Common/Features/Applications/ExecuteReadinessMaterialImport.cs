using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Records the execute readiness material import intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.imports.execute-readiness-material-import", 1)]
public sealed record ExecuteReadinessMaterialImport(Uuid TenantId, Uuid ImportId, string ExecutionKey) : IRequest, IClientManagementMutationRequest;

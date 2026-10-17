using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Records the stage readiness material import intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.imports.stage-readiness-material-import", 1)]
public sealed record StageReadinessMaterialImport(Uuid TenantId, Uuid ImportId, string SourceName, string SourceIdentity, string FileName, byte[] Content) : IRequest, IClientManagementMutationRequest, ICallable;

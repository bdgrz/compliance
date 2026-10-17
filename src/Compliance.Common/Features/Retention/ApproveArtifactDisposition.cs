using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

/// <summary>Records the approve artifact disposition intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.retention.approve-artifact-disposition", 1)]
public sealed record ApproveArtifactDisposition(Uuid TenantId, Uuid DispositionId, Uuid ArtifactId, string Rationale) : IRequest, IClientManagementMutationRequest, ICallable;

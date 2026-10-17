using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

/// <summary>Records the assign artifact handling policy intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.retention.assign-artifact-handling-policy", 1)]
public sealed record AssignArtifactHandlingPolicy(Uuid TenantId, Uuid ArtifactId, long ExpectedRevision, string HandlingClass, long PolicyVersion) : IRequest, IClientManagementMutationRequest, ICallable;

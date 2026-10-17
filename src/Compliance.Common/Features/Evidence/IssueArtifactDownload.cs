using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Authorizes and records an attributable download of one available artifact.</summary>
[Discriminator("bdgrz.evidence.artifact.download.issue", 1)]
public sealed record IssueArtifactDownload(Uuid TenantId, Uuid ArtifactId, string Purpose)
    : IRequest, IClientManagementMutationRequest, ICallable;

using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Uploads and registers one evidence artifact for governed inspection and storage.</summary>
[Discriminator("bdgrz.evidence.artifact.capture", 1)]
public sealed record CaptureEvidenceArtifact(Uuid TenantId, byte[] Content, EvidenceArtifactContent Metadata)
    : IRequest<EvidenceCapture>, IClientManagementMutationRequest, ICallable;

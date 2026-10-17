using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

/// <summary>Records the approve artifact disclosure intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.retention.approve-artifact-disclosure", 1)]
public sealed record ApproveArtifactDisclosure(Uuid TenantId, Uuid DisclosureId, Uuid ArtifactId, string Recipient, string Purpose, long ArtifactRevision, string Rationale) : IRequest, IClientManagementMutationRequest, ICallable;

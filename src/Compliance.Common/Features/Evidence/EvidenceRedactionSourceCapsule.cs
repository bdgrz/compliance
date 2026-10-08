using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Immutable source identity only; it never copies original content metadata or bytes.</summary>
public sealed record EvidenceRedactionSourceCapsule(Uuid TenantId, Uuid ArtifactId,
    Uuid RegistrationEventId, string RegistrationSha256, string ContentSha256,
    long ContentLength, DateTimeOffset RegisteredAt, string IdentitySha256);

using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Internal exact registration binding; the opaque payload digest supplies no access or disclosure authority.</summary>
sealed record EvidenceArtifactMetadataSnapshot(EvidenceArtifactMetadataView Metadata,
    Uuid RegistrationEventId, string RegistrationPayloadSha256, DateTimeOffset? AvailabilityRecordedAt);

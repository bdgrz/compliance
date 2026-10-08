using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

[Discriminator("bdgrz.artifact_retention.basis_recorded", 1)]
public sealed record ArtifactRetentionBasisRecorded(ArtifactRetentionSource Source, long Revision, ulong SourcePosition,
    string Policy, DateOnly PeriodStart, DateOnly PeriodEnd, DateOnly RetainsThrough, string BasisKind, string Reason,
    Bdgrz.Compliance.Features.AccessControl.ActorReference RecordedBy, DateTimeOffset RecordedAt) : DomainEvent;

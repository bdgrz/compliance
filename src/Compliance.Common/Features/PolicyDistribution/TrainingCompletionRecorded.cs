using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     A reviewed completion observation from <c>manual</c> evidence or an <c>lms_export</c> row.
///     The evidence reference names the source artifact.
/// </summary>
[Discriminator("bdgrz.training_completion.recorded", 1)]
public sealed record TrainingCompletionRecorded(Uuid TenantId, Uuid CampaignId,
    Uuid CompletionId, Uuid PersonId, Uuid RequirementId, long RequirementVersion,
    DateOnly CompletedOn, string Source, string EvidenceReference, ActorReference Recorder,
    DateTimeOffset RecordedAt) : DomainEvent;

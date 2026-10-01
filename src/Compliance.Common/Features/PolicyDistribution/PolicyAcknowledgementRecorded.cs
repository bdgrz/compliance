using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     A person's acknowledgement of the exact policy version, hash, and text shown.
///     <c>Performer</c> is the workforce person; <c>Recorder</c> is the member who recorded it,
///     which differs only when a member records it for a non-member.
/// </summary>
[Discriminator("bdgrz.policy.acknowledgement.recorded", 1)]
public sealed record PolicyAcknowledgementRecorded(Uuid TenantId, Uuid CampaignId,
    Uuid AcknowledgementId, Uuid PersonId, Uuid PolicyId, long PolicyVersion,
    string ContentSha256, string AcknowledgementText, ActorReference Performer,
    ActorReference Recorder, bool RecordedOnBehalf, DateTimeOffset AcknowledgedAt)
    : DomainEvent;

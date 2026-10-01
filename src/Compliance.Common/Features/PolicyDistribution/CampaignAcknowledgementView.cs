using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

public sealed record CampaignAcknowledgementView(Uuid AcknowledgementId, Uuid PersonId,
    long PolicyVersion, string ContentSha256, string AcknowledgementText,
    ActorReference Performer, ActorReference Recorder, bool RecordedOnBehalf,
    DateTimeOffset AcknowledgedAt);

using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

static class PolicyCampaignWorkItemDirectorySchema
{
    public static readonly KvDirectory<AccountableWorkItemProjectionRevision, string> Revisions =
        new("projection_revisions",
            ComplianceCoreJsonContext.Default.AccountableWorkItemProjectionRevision,
            static revision => revision.ProjectorName,
            static projectorName => [projectorName], []);

    public static readonly KvDirectoryIndex<PolicyCampaignWorkCampaignState> CampaignsByProgram =
        new("by_program", 1, static campaign =>
            [campaign.ProgramId.ToString(), campaign.CampaignId.ToString()]);

    public static readonly KvDirectory<PolicyCampaignWorkCampaignState, Uuid> Campaigns = new(
        "campaigns", ComplianceCoreJsonContext.Default.PolicyCampaignWorkCampaignState,
        static campaign => campaign.CampaignId,
        static campaignId => [campaignId.ToString()], [CampaignsByProgram]);

    public static readonly KvDirectoryIndex<PolicyCampaignWorkParticipantState> ParticipantsByCampaign =
        new("by_program_campaign", 1, static participant =>
            [participant.ProgramId.ToString(), participant.CampaignId.ToString(),
             participant.PersonId.ToString()]);

    public static readonly KvDirectory<PolicyCampaignWorkParticipantState, string> Participants = new(
        "participants", ComplianceCoreJsonContext.Default.PolicyCampaignWorkParticipantState,
        static participant => Key(participant.CampaignId, participant.PersonId),
        static key => [key], [ParticipantsByCampaign]);

    public static string Key(Uuid campaignId, Uuid personId) =>
        $"{campaignId}:{personId}";
}

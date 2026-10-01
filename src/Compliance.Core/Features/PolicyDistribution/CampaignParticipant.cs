using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>The mutable replay state of one person in a campaign's evidence population.</summary>
sealed class CampaignParticipant(Uuid personId, string displayName, string workerType,
    string? department, bool launchMember, string inclusion, Uuid includedBy, DateOnly dueOn)
{
    public Uuid PersonId { get; } = personId;
    public string DisplayName { get; set; } = displayName;
    public string WorkerType { get; set; } = workerType;
    public string? Department { get; set; } = department;
    public bool LaunchMember { get; } = launchMember;
    public string Inclusion { get; set; } = inclusion;
    public Uuid IncludedBy { get; set; } = includedBy;
    public DateOnly DueOn { get; set; } = dueOn;
    public string? RemovedReason { get; set; }
    public Uuid? RemovedBy { get; set; }
    public CampaignAcknowledgementView? Acknowledgement { get; set; }
    public CampaignCompletionView? Completion { get; set; }
    public CampaignWaiverView? Waiver { get; set; }
    public bool IsActive => RemovedReason is null;
}

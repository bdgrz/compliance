using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     Launches acknowledgement of the exact current approved policy version, freezing the
///     audience from the given roster snapshot.
/// </summary>
[Discriminator("bdgrz.policy_campaign.launch", 1)]
public sealed record LaunchPolicyCampaign(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    long PolicyVersion, Uuid RosterSnapshotId, DateOnly DueOn, string? Instructions = null)
    : IRequest<CampaignRegistration>, IProgramScopedRequest, ICallable;

using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

[Discriminator("bdgrz.training_campaign.launch", 1)]
public sealed record LaunchTrainingCampaign(Uuid TenantId, Uuid ProgramId,
    Uuid RequirementId, long RequirementVersion, Uuid RosterSnapshotId, DateOnly DueOn,
    string? Instructions = null)
    : IRequest<CampaignRegistration>, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;

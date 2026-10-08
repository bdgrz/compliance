using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>Records a reviewed manual or LMS-export completion for one person.</summary>
[Discriminator("bdgrz.training_completion.record", 1)]
public sealed record RecordTrainingCompletion(Uuid TenantId, Uuid ProgramId, Uuid CampaignId,
    Uuid PersonId, long RequirementVersion, DateOnly CompletedOn, string Source,
    string EvidenceReference)
    : IRequest<CampaignCompletionView>, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;

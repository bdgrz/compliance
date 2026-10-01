using Bdgrz.Compliance.Features.Policies;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>Launches completion tracking for the exact latest training requirement version.</summary>
public sealed class LaunchTrainingCampaignHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<LaunchTrainingCampaign, CampaignRegistration>
{
    public async ValueTask<Result<CampaignRegistration>> HandleAsync(
        IRequestContext<LaunchTrainingCampaign> context, CancellationToken ct)
    {
        var request = context.Request;
        var catalog = await reader.HydrateAsync(new TrainingCatalog(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        if (catalog.Find(request.RequirementId, request.RequirementVersion) is not { } requirement)
            return Result<CampaignRegistration>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The training requirement version was not found."));
        if (requirement.Version != requirement.LatestVersion)
            return Result<CampaignRegistration>.Failure(new RequestError(
                RequestErrorKind.Conflict,
                "Only the latest training requirement version can be launched."));
        var roster = await RosterSnapshotSource.ReadAsync(reader, request.TenantId,
            request.RosterSnapshotId, ct).ConfigureAwait(false);
        if (!roster.IsSuccess)
            return Result<CampaignRegistration>.Failure(roster.Error);
        var content = requirement.Content;
        var teams = content.AudienceTeams ?? [];
        var audience = RosterAudience.Evaluate(roster.Value.Roster, content.AudienceKind, teams);
        var actor = PolicyActor.From(context, request.TenantId);
        var subject = new CampaignSubject("training", requirement.RequirementId,
            requirement.Identifier, content.CourseName, requirement.Version,
            requirement.ContentSha256);
        return await executor.ExecuteAsync(new PolicyDistributionCampaign(request.TenantId,
                context.RequestId),
            campaign => AggregateOutcome.CommitOnSuccess(campaign.Launch(request.ProgramId,
                subject, content.AudienceKind, teams, request.RosterSnapshotId,
                roster.Value.Roster.ContentSha256, audience, request.DueOn, request.Instructions,
                actor.Reference, actor.MemberId, clock.GetUtcNow())),
            context, ct).ConfigureAwait(false);
    }
}

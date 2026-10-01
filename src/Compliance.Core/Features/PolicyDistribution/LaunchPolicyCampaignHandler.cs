using Bdgrz.Compliance.Features.Policies;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>Launches acknowledgement of the exact current approved policy version.</summary>
public sealed class LaunchPolicyCampaignHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<LaunchPolicyCampaign, CampaignRegistration>
{
    public async ValueTask<Result<CampaignRegistration>> HandleAsync(
        IRequestContext<LaunchPolicyCampaign> context, CancellationToken ct)
    {
        var request = context.Request;
        var policy = await PolicySource.ReadAsync(reader, request.TenantId, request.ProgramId,
            request.PolicyId, ct).ConfigureAwait(false);
        if (!policy.IsSuccess)
            return Result<CampaignRegistration>.Failure(policy.Error);
        if (policy.Value.FindVersion(request.PolicyVersion) is not { } version)
            return Result<CampaignRegistration>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The policy version was not found."));
        if (version.Status != "approved")
            return Result<CampaignRegistration>.Failure(new RequestError(
                RequestErrorKind.Conflict,
                "Only the current approved policy version can be distributed."));
        var roster = await RosterSnapshotSource.ReadAsync(reader, request.TenantId,
            request.RosterSnapshotId, ct).ConfigureAwait(false);
        if (!roster.IsSuccess)
            return Result<CampaignRegistration>.Failure(roster.Error);
        var content = version.Content;
        var teams = content.AudienceTeams ?? [];
        var audience = RosterAudience.Evaluate(roster.Value.Roster, content.AudienceKind, teams);
        var actor = PolicyActor.From(context, request.TenantId);
        var subject = new CampaignSubject("policy", version.PolicyId, version.Identifier,
            content.Title, version.Version, version.ContentSha256);
        return await executor.ExecuteAsync(new PolicyDistributionCampaign(request.TenantId,
                context.RequestId),
            campaign => AggregateOutcome.CommitOnSuccess(campaign.Launch(request.ProgramId,
                subject, content.AudienceKind, teams, request.RosterSnapshotId,
                roster.Value.Roster.ContentSha256, audience, request.DueOn, request.Instructions,
                actor.Reference, actor.MemberId, clock.GetUtcNow())),
            context, ct).ConfigureAwait(false);
    }
}

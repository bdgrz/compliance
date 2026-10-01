using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

sealed class FitzCampaignDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/policy-campaign-directory/projection",
            ProjectorName),
        ICampaignDirectoryReader, ICampaignDirectoryProjection
{
    public const string ProjectorName = "PolicyCampaignDirectoryV1";

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity(ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "policy-distribution-campaigns")),
            ct);

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case PolicyCampaignLaunched launched:
                await CampaignDirectorySchema.Campaigns.InsertAsync(Transaction,
                    new CampaignSummaryView(launched.TenantId, launched.ProgramId,
                        launched.CampaignId, launched.Subject.Kind, launched.Subject.RecordId,
                        launched.Subject.Identifier, launched.Subject.Version,
                        launched.Subject.Title, launched.DueOn, "open", launched.AudienceCount,
                        launched.LaunchedAt), ct).ConfigureAwait(false);
                break;
            case PolicyCampaignClosed closed:
                var current = await CampaignDirectorySchema.Campaigns.GetAsync(Transaction,
                    closed.CampaignId, ct).ConfigureAwait(false) ??
                              throw new InvalidOperationException(
                                  "A campaign cannot close before it launched.");
                await CampaignDirectorySchema.Campaigns.ReplaceAsync(Transaction, current,
                    current with { Status = "closed" }, ct).ConfigureAwait(false);
                break;
        }
    }

    public async ValueTask<Page<CampaignSummaryView>> ListProgramAsync(Uuid tenantId,
        Uuid programId, int limit, string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await CampaignDirectorySchema.Campaigns.QueryAsync(tx,
            CampaignDirectorySchema.ByProgramLaunch.Query()
                .WithPrefix(programId.ToString()).Take(Math.Clamp(limit, 1, 200))
                .After(cursor), ct).ConfigureAwait(false);
    }

    public async ValueTask<IReadOnlyList<Uuid>> ListForSubjectAsync(Uuid tenantId,
        Uuid programId, Uuid subjectId, long version, CancellationToken ct = default)
    {
        var matches = new List<Uuid>();
        string? cursor = null;
        do
        {
            var page = await ListProgramAsync(tenantId, programId, 200, cursor, ct)
                .ConfigureAwait(false);
            matches.AddRange(page.Items.Where(campaign => campaign.SubjectId == subjectId &&
                    campaign.SubjectVersion == version)
                .Select(static campaign => campaign.CampaignId));
            cursor = page.NextCursor;
        } while (cursor is not null);
        return matches;
    }
}

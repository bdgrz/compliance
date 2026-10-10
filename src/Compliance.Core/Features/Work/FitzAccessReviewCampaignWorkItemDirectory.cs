using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

sealed class FitzAccessReviewCampaignWorkItemDirectory(IKvClient client,
    IAggregateReader sourceReader)
    : FitzKvProjectionStore(client,
        "kv://bdgrz/accountable-work-items/access-review-campaigns-v1", ProjectorName),
      IAccountableWorkItemDirectoryReader, IAccessReviewCampaignWorkItemProjection
{
    public const string ProjectorName = "AccountableWorkItemAccessReviewCampaignV1";
    const string RevisionKey = ProjectorName;

    string IAccountableWorkItemDirectoryReader.ProjectorName => ProjectorName;

    public IReadOnlyCollection<string> ProjectedKinds => AccessReviewCampaignWork.ProjectedKinds;

    public EventStreamPattern SourcePattern(Uuid tenantId) =>
        EventStreamPattern.ForPattern(tenantId.ToString(), AccessReviewCampaign.Area);

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
        CancellationToken ct = default) => base.LoadCheckpointAsync(new CheckpointIdentity(
        ProjectorName, SourcePattern(tenantId)), ct);

    public async ValueTask<long> LoadRevisionAsync(Uuid tenantId,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return (await AccessReviewCampaignWorkItemDirectorySchema.Revisions.GetAsync(tx,
            RevisionKey, ct).ConfigureAwait(false))?.Revision ?? 0;
    }

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        var (tenantId, campaignId, eventRevision) = ScopeFor(domainEvent);
        if (tenantId == Uuid.Empty || campaignId == Uuid.Empty)
            throw new InvalidOperationException(
                "An access-review campaign work event must have complete tenant and campaign scope.");

        var campaign = await sourceReader.HydrateAsync(
            new AccessReviewCampaign(tenantId, campaignId), ct).ConfigureAwait(false);
        if (!campaign.IsLaunched || campaign.Revision < eventRevision || campaign.Revision < 1 ||
            campaign.Launched is not { } launched || launched.TenantId != tenantId ||
            launched.CampaignId != campaignId)
            throw new InvalidOperationException(
                "An access-review work event must match its authoritative source aggregate.");

        var current = await AccessReviewCampaignWorkItemDirectorySchema.Campaigns.GetAsync(
            Transaction, campaignId, ct).ConfigureAwait(false);
        if (current is not null && (current.TenantId != tenantId ||
            current.CampaignId != campaignId))
            throw new InvalidOperationException(
                "An access-review campaign cannot replace work state outside its recorded scope.");

        if (launched.ProgramId is null && launched.RemediationOwnerMemberId is null)
        {
            if (current is not null)
                throw new InvalidOperationException(
                    "A routed access-review campaign cannot lose its frozen Program or remediation owner.");
            return;
        }

        if (launched.ProgramId is not { } programId ||
            launched.RemediationOwnerMemberId is not { } remediationOwnerId)
            throw new InvalidOperationException(
                "A routed access-review campaign must freeze both its Program and remediation owner.");

        if (programId == Uuid.Empty || remediationOwnerId == Uuid.Empty ||
            launched.Items.Count == 0 || campaign.Revision < 1)
            throw new InvalidOperationException(
                "A routed access-review campaign has incomplete work projection state.");

        var program = await sourceReader.HydrateAsync(new ComplianceProgram(tenantId, programId), ct)
            .ConfigureAwait(false);
        if (!program.IsCreated)
            throw new InvalidOperationException(
                "A routed access-review campaign must reference a created Program in its tenant.");

        if (current is not null && current.ProgramId != programId)
            throw new InvalidOperationException(
                "A campaign's frozen Program cannot change after launch.");

        if (current is not null && current.SourceRevision > campaign.Revision)
            return;
        if (current is not null && current.SourceRevision == campaign.Revision)
            return;

        var view = campaign.ToView(DateTimeOffset.UtcNow);
        var nextCampaign = new AccessReviewCampaignWorkCampaignState(tenantId, programId,
            campaignId, campaign.Revision, DateOnly.FromDateTime(launched.Deadline.UtcDateTime),
            launched.LaunchedAt, campaign.Completion is not null, view.Items.Count);

        if (current is null)
            await AccessReviewCampaignWorkItemDirectorySchema.Campaigns.InsertAsync(Transaction,
                nextCampaign, ct).ConfigureAwait(false);
        else
            await AccessReviewCampaignWorkItemDirectorySchema.Campaigns.ReplaceAsync(Transaction,
                current, nextCampaign, ct).ConfigureAwait(false);

        var desiredItemIds = view.Items.Select(static item => item.Item.ItemId).ToHashSet();
        var staleItems = await ReadCampaignItemsAsync(programId, campaignId, ct)
            .ConfigureAwait(false);
        foreach (var stale in staleItems.Where(item => !desiredItemIds.Contains(item.ItemId)))
            await AccessReviewCampaignWorkItemDirectorySchema.Items.DeleteAsync(Transaction,
                stale, ct).ConfigureAwait(false);

        foreach (var item in view.Items)
        {
            if (item.Item.ItemId == Uuid.Empty || item.Item.SystemInstanceId == Uuid.Empty)
                throw new InvalidOperationException(
                    "A frozen campaign item has incomplete work scope.");
            var next = new AccessReviewCampaignWorkItemState(tenantId, programId, campaignId,
                item.Item.ItemId, campaign.Revision, nextCampaign.DueOn, launched.LaunchedAt,
                nextCampaign.IsCompleted, item.Item.SystemInstanceId,
                item.Item.SubjectUserId is { } subjectUserId
                    ? RbacIds.Member(tenantId, subjectUserId)
                    : null,
                item.Item.Privileged,
                item.Decision?.Decision, item.Verification is not null, item.Exception is not null,
                item.Exception?.ExpiresAt, item.CurrentReviewerMemberId,
                item.CurrentRemediationOwnerMemberId, item.ProviderChanges.Count > 0);
            var existing = await AccessReviewCampaignWorkItemDirectorySchema.Items.GetAsync(
                Transaction, AccessReviewCampaignWorkItemDirectorySchema.ItemKey(campaignId,
                    item.Item.ItemId), ct).ConfigureAwait(false);
            if (existing is null)
                await AccessReviewCampaignWorkItemDirectorySchema.Items.InsertAsync(Transaction,
                    next, ct).ConfigureAwait(false);
            else if (existing.TenantId != tenantId || existing.ProgramId != programId ||
                     existing.CampaignId != campaignId || existing.ItemId != item.Item.ItemId)
                throw new InvalidOperationException(
                    "An access-review item cannot replace work state outside its recorded scope.");
            else
                await AccessReviewCampaignWorkItemDirectorySchema.Items.ReplaceAsync(Transaction,
                    existing, next, ct).ConfigureAwait(false);
        }

        await IncrementRevisionAsync(ct).ConfigureAwait(false);
    }

    public ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId,
        Uuid programId, CancellationToken ct = default) =>
        LoadProgramAsync(tenantId, programId, DateTimeOffset.UtcNow, DateOnly.MaxValue, null, ct);

    public ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId,
        Uuid programId, DateOnly today, DateOnly horizon, Uuid? workItemId,
        CancellationToken ct = default) => LoadProgramAsync(tenantId, programId,
        new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), horizon,
        workItemId, ct);

    public async ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId,
        Uuid programId, DateTimeOffset now, DateOnly horizon, Uuid? workItemId,
        CancellationToken ct = default)
    {
        var candidates = new List<WorkCandidate>();
        string? cursor = null;
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        do
        {
            var page = await AccessReviewCampaignWorkItemDirectorySchema.Items.QueryAsync(tx,
                AccessReviewCampaignWorkItemDirectorySchema.ItemsByProgram.Query()
                    .WithPrefix(programId.ToString()).Take(200).After(cursor), ct)
                .ConfigureAwait(false);
            foreach (var state in page.Items)
            {
                if (state.TenantId != tenantId || state.ProgramId != programId ||
                    state.CampaignId == Uuid.Empty || state.ItemId == Uuid.Empty ||
                    state.SourceRevision < 1)
                    return Result<IReadOnlyList<WorkCandidate>>.Failure(InvalidScope());
                if (state.IsCompleted || state.DueOn > horizon)
                    continue;

                candidates.AddRange(AccessReviewCampaignWork.Candidates(state, now, workItemId));
            }
            cursor = page.NextCursor;
        } while (cursor is not null);

        return Result<IReadOnlyList<WorkCandidate>>.Success(candidates);
    }

    async ValueTask<IReadOnlyList<AccessReviewCampaignWorkItemState>> ReadCampaignItemsAsync(
        Uuid programId, Uuid campaignId, CancellationToken ct)
    {
        var items = new List<AccessReviewCampaignWorkItemState>();
        string? cursor = null;
        do
        {
            var page = await AccessReviewCampaignWorkItemDirectorySchema.Items.QueryAsync(Transaction,
                AccessReviewCampaignWorkItemDirectorySchema.ItemsByProgram.Query()
                    .WithPrefix(programId.ToString(), campaignId.ToString()).Take(200).After(cursor),
                ct).ConfigureAwait(false);
            items.AddRange(page.Items);
            cursor = page.NextCursor;
        } while (cursor is not null);
        return items;
    }

    async ValueTask IncrementRevisionAsync(CancellationToken ct)
    {
        var current = await AccessReviewCampaignWorkItemDirectorySchema.Revisions.GetAsync(
            Transaction, RevisionKey, ct).ConfigureAwait(false);
        var next = new AccountableWorkItemProjectionRevision(ProjectorName,
            checked((current?.Revision ?? 0) + 1));
        if (current is null)
            await AccessReviewCampaignWorkItemDirectorySchema.Revisions.InsertAsync(Transaction,
                next, ct).ConfigureAwait(false);
        else
            await AccessReviewCampaignWorkItemDirectorySchema.Revisions.ReplaceAsync(Transaction,
                current, next, ct).ConfigureAwait(false);
    }

    static (Uuid TenantId, Uuid CampaignId, long Revision) ScopeFor(DomainEvent domainEvent) =>
        domainEvent switch
        {
            AccessReviewCampaignLaunched ev => (ev.TenantId, ev.CampaignId, 1),
            AccessDecisionsRecorded ev => (ev.TenantId, ev.CampaignId, ev.Revision),
            AccessRemediationChangeRecorded ev => (ev.TenantId, ev.CampaignId, ev.Revision),
            AccessRemediationVerified ev => (ev.TenantId, ev.CampaignId, ev.Revision),
            AccessRemediationExceptionRecorded ev => (ev.TenantId, ev.CampaignId, ev.Revision),
            AccessReviewResponsibilityReassigned ev => (ev.TenantId, ev.CampaignId, ev.Revision),
            AccessReviewCampaignCompleted ev => (ev.TenantId, ev.CampaignId, ev.Revision),
            _ => (Uuid.Empty, Uuid.Empty, 0),
        };

    static RequestError InvalidScope() => new(RequestErrorKind.Conflict,
        "The access-review campaign work projection has an invalid Program scope.");
}

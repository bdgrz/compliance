using System.Text.Json;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

sealed class FitzAssuranceDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/provider-assurance-v1/projection", ProjectorName), IAssuranceReader, IAssuranceProjection
{
    public const string ProjectorName = "ProviderAssuranceV1";
    Uuid? _batchTenantId;

    public new ValueTask<IProjectionBatch> BeginAsync(ProjectionBatchContext context,
        CancellationToken ct = default) => BeginAssuranceBatchAsync(context, ct);

    ValueTask<IProjectionBatch> IProjectionStore.BeginAsync(ProjectionBatchContext context,
        CancellationToken ct) => BeginAssuranceBatchAsync(context, ct);

    async ValueTask<IProjectionBatch> BeginAssuranceBatchAsync(ProjectionBatchContext context, CancellationToken ct)
    {
        if (context.Identity.Pattern.Area != ProviderAssuranceRegister.Area ||
            context.Identity.Pattern.Resource is not null ||
            !Uuid.TryParse(context.Identity.Pattern.Realm, null, out var tenantId) || tenantId == Uuid.Empty)
            throw new InvalidOperationException("An assurance projection batch requires its tenant's assurance area.");
        var batch = await base.BeginAsync(context, ct).ConfigureAwait(false);
        _batchTenantId = tenantId;
        return batch;
    }

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity(ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), ProviderAssuranceRegister.Area)), ct);

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case AssuranceReportRecorded ev:
                await ApplyReportAsync(View(ev.TenantId, ev.ReportId, ev.ProviderId, 1, ev.Content, ev.ProviderRevision,
                    ev.Actor, ev.RecordedAt), ct).ConfigureAwait(false);
                break;
            case AssuranceReportRevised { Revision: >= 2 } ev:
                await ApplyReportAsync(View(ev.TenantId, ev.ReportId, ev.ProviderId, ev.Revision, ev.Content,
                    ev.ProviderRevision, ev.Actor, ev.RecordedAt), ct).ConfigureAwait(false);
                break;
            case ProviderReviewRecorded ev:
                await ApplyReviewAsync(new ProviderReviewView(ev.TenantId, ev.ReviewId, ev.ProviderId,
                    AssuranceRules.Normalize(ev.Content), ev.ProviderRevision, ev.AssuranceReportRevision, ev.Actor,
                    ev.RecordedAt), ct).ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException("The assurance event is not supported.");
        }
    }

    public async ValueTask<Page<AssuranceReportView>> ListReportsAsync(Uuid tenantId, Uuid providerId, int limit,
        string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await AssuranceDirectorySchema.Reports.QueryAsync(tx,
            AssuranceDirectorySchema.ReportsByProvider.Query().WithPrefix(providerId.ToString()).Take(limit).After(cursor), ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<Page<ProviderReviewView>> ListReviewsAsync(Uuid tenantId, Uuid providerId, int limit,
        string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await AssuranceDirectorySchema.Reviews.QueryAsync(tx,
            AssuranceDirectorySchema.ReviewsByProvider.Query().WithPrefix(providerId.ToString()).Take(limit).After(cursor), ct)
            .ConfigureAwait(false);
    }

    async ValueTask ApplyReportAsync(AssuranceReportView view, CancellationToken ct)
    {
        RequireBatchTenant(view.TenantId, view.ProviderId, view.ReportId);
        var current = await AssuranceDirectorySchema.Reports.GetAsync(Transaction, view.ReportId, ct).ConfigureAwait(false);
        if (current is not null && current.Revision >= view.Revision)
        {
            // A replayed revision is idempotent only when it matches the retained current or a superseded fact.
            if (current.Revision == view.Revision && !Same(current, view))
                throw new InvalidOperationException("An assurance report revision cannot replace retained content.");
            return;
        }
        if (current is null ? view.Revision != 1 : current.TenantId != view.TenantId ||
                current.ProviderId != view.ProviderId || current.Revision + 1 != view.Revision)
            throw new InvalidOperationException("An assurance report revision requires its tenant's immediate predecessor.");
        if (current is null)
            await AssuranceDirectorySchema.Reports.InsertAsync(Transaction, view, ct).ConfigureAwait(false);
        else
            await AssuranceDirectorySchema.Reports.ReplaceAsync(Transaction, current, view, ct).ConfigureAwait(false);
    }

    async ValueTask ApplyReviewAsync(ProviderReviewView view, CancellationToken ct)
    {
        RequireBatchTenant(view.TenantId, view.ProviderId, view.ReviewId);
        var retained = await AssuranceDirectorySchema.Reviews.GetAsync(Transaction, view.ReviewId, ct).ConfigureAwait(false);
        if (retained is not null)
        {
            if (!Same(retained, view))
                throw new InvalidOperationException("A provider review cannot replace retained content.");
            return;
        }
        await AssuranceDirectorySchema.Reviews.InsertAsync(Transaction, view, ct).ConfigureAwait(false);
    }

    void RequireBatchTenant(Uuid tenantId, Uuid providerId, Uuid recordId)
    {
        if (tenantId == Uuid.Empty || providerId == Uuid.Empty || recordId == Uuid.Empty || tenantId != _batchTenantId)
            throw new InvalidOperationException("An assurance event must belong to its tenant projection batch.");
    }

    static AssuranceReportView View(Uuid tenantId, Uuid reportId, Uuid providerId, long revision,
        AssuranceReportContent content, long providerRevision, Bdgrz.Compliance.Features.AccessControl.ActorReference actor,
        DateTimeOffset recordedAt)
    {
        content = AssuranceRules.Normalize(content);
        return new AssuranceReportView(tenantId, reportId, providerId, revision, content, providerRevision,
            content.Exceptions!.Count, content.ComplementaryControls!.Count, content.CoverageGaps!.Count, actor, recordedAt);
    }

    static bool Same(AssuranceReportView left, AssuranceReportView right) =>
        JsonSerializer.SerializeToUtf8Bytes(left, ComplianceCoreJsonContext.Default.AssuranceReportView).AsSpan()
            .SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(right, ComplianceCoreJsonContext.Default.AssuranceReportView));

    static bool Same(ProviderReviewView left, ProviderReviewView right) =>
        JsonSerializer.SerializeToUtf8Bytes(left, ComplianceCoreJsonContext.Default.ProviderReviewView).AsSpan()
            .SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(right, ComplianceCoreJsonContext.Default.ProviderReviewView));
}

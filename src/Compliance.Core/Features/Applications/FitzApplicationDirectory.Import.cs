using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

sealed partial class FitzApplicationDirectory
{
    Uuid? _batchTenantId;

    public new ValueTask<IProjectionBatch> BeginAsync(ProjectionBatchContext context, CancellationToken ct = default) =>
        BeginApplicationBatchAsync(context, ct);

    ValueTask<IProjectionBatch> IProjectionStore.BeginAsync(ProjectionBatchContext context, CancellationToken ct) =>
        BeginApplicationBatchAsync(context, ct);

    async ValueTask<IProjectionBatch> BeginApplicationBatchAsync(ProjectionBatchContext context, CancellationToken ct)
    {
        if (context.Identity.Pattern.Resource is not null ||
            !Uuid.TryParse(context.Identity.Pattern.Realm, null, out var tenantId) || tenantId == Uuid.Empty)
            throw new InvalidOperationException("An application projection batch requires its tenant realm.");
        var batch = await base.BeginAsync(context, ct).ConfigureAwait(false);
        _batchTenantId = tenantId;
        return batch;
    }

    public async ValueTask ApplyCommittedImportAsync(ApplicationImportCommitted marker, ApplicationImportFrozenPlan plan,
        CancellationToken ct = default)
    {
        if (marker.TenantId != _batchTenantId || marker.TenantId != plan.Start.TenantId || marker.BatchId != plan.Start.BatchId ||
            marker.SourceKey != plan.Start.SourceKey || marker.SourceNamespace != plan.Start.SourceNamespace ||
            marker.PlanSha256 != plan.PlanSha256 || marker.Revision != plan.Revision + 1 ||
            marker.Effects.Count != plan.Rows.Count ||
            plan.Rows.Any(row => marker.Effects.Count(proof => proof.RowId == row.RowId &&
                proof.ApplicationId == row.ApplicationId) != 1))
            throw new InvalidOperationException("The import projection requires its complete committed frozen plan.");
        foreach (var row in plan.Rows)
        {
            if (row.Decision == "link_existing")
            {
                var linked = await RequireApplicationAsync(row.ApplicationId, ct).ConfigureAwait(false);
                if (linked.TenantId != marker.TenantId || linked.Revision != row.ExpectedApplicationRevision ||
                    linked.Lifecycle != "active")
                    throw new InvalidOperationException("The linked import target differs from its frozen governed revision.");
                continue;
            }
            if (row.Decision == "retire")
            {
                var retiring = await RequireApplicationAsync(row.ApplicationId, ct).ConfigureAwait(false);
                if (retiring.TenantId != marker.TenantId || retiring.Revision != row.ExpectedApplicationRevision ||
                    retiring.Lifecycle != "active" || string.IsNullOrWhiteSpace(row.RetirementReason))
                    throw new InvalidOperationException("The retirement import target differs from its frozen governed revision.");
                var retired = retiring with
                {
                    Revision = checked(retiring.Revision + 1),
                    LastChangedByMemberId = plan.Start.ApproverMemberId,
                    LastChangedByDisplay = plan.Start.ApproverDisplay,
                    LastChangedAt = marker.CommittedAt,
                    LastChangedBy = ActorReference.ForMember(plan.Start.ApproverMemberId,
                        plan.Start.ApproverDisplay),
                    Lifecycle = "retired",
                    Retirement = new RetirementView(marker.CommittedAt, row.RetirementReason, null),
                };
                await ApplicationDirectorySchema.Applications.ReplaceAsync(Transaction, retiring,
                    retired, ct).ConfigureAwait(false);
                await InsertRevisionAsync(retired, "retired", null, ct).ConfigureAwait(false);
                continue;
            }
            if (row.Decision != "create_new")
                throw new InvalidOperationException("The import projection has an unsupported row decision.");
            var view = new ApplicationView(marker.TenantId, row.ApplicationId, 1, row.Name.Trim(),
                row.Purpose.Trim(), NormalizeImportOwner(row.OwnerReference), "import", row.SourceRecordId,
                false, Gaps(row.OwnerReference, null, false, null, null), plan.Start.ApproverMemberId,
                plan.Start.ApproverDisplay, marker.CommittedAt)
            {
                LastChangedBy = ActorReference.ForMember(plan.Start.ApproverMemberId, plan.Start.ApproverDisplay),
            };
            await ApplicationDirectorySchema.Applications.InsertAsync(Transaction, view, ct).ConfigureAwait(false);
            await InsertRevisionAsync(view, "imported", null, ct).ConfigureAwait(false);
        }
        var epoch = await ApplicationDirectorySchema.ImportEpoch.GetAsync(Transaction, "imports", ct).ConfigureAwait(false);
        var next = new ApplicationImportVisibilityEpoch(checked((epoch?.Revision ?? 0) + 1));
        if (epoch is null)
            await ApplicationDirectorySchema.ImportEpoch.InsertAsync(Transaction, next, ct).ConfigureAwait(false);
        else
            await ApplicationDirectorySchema.ImportEpoch.ReplaceAsync(Transaction, epoch, next, ct).ConfigureAwait(false);
    }

    static string? NormalizeImportOwner(string? owner) => string.IsNullOrWhiteSpace(owner) ? null : owner.Trim();
}

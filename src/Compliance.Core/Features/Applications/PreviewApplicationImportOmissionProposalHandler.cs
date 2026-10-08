using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class PreviewApplicationImportOmissionProposalHandler(IAggregateReader reader)
    : IRequestHandler<PreviewApplicationImportOmissionProposal, ApplicationImportOmissionPreview>
{
    public async ValueTask<Result<ApplicationImportOmissionPreview>> HandleAsync(
        IRequestContext<PreviewApplicationImportOmissionProposal> context, CancellationToken ct)
    {
        var request = context.Request;
        var loaded = await ApplicationImportOmissionSources.LoadAsync(reader, request.TenantId, request.BatchId, ct).ConfigureAwait(false);
        if (!loaded.IsSuccess)
            return Result<ApplicationImportOmissionPreview>.Failure(loaded.Error);
        var (batch, ledger) = loaded.Value;
        var missing = ledger.GetMissingSourceClaims(batch);
        if (!missing.IsSuccess)
            return Result<ApplicationImportOmissionPreview>.Failure(missing.Error);
        if (missing.Value.Count > 200)
            return Result<ApplicationImportOmissionPreview>.Failure(new RequestError(RequestErrorKind.Validation,
                "An omission proposal preview cannot contain more than 200 missing claims."));
        var rows = missing.Value.Select(claim => new ApplicationImportRetirementRow(
            Uuid.CreateVersion5(ledger.Id, $"application_import_claim:{claim.Observation.SourceRecordId}"),
            claim.Observation.SourceRecordId, claim.Observation.ApplicationId, claim.Plan.BatchId,
            claim.Observation.ExpectedApplicationRevision ?? 1, claim.CommittedAt)).ToArray();
        var blockers = ApplicationImportOmissionSources.Blockers.ToList();
        if (ledger.HasSharedOrPresentRetirementAlias(batch, missing.Value))
            blockers.Add("source_alias_cannot_propose_retirement");
        if (batch.Coverage != "declared_complete")
            blockers.Add("partial_source_cannot_propose_retirement");
        if (batch.GetRows().Any(row => row.ValidationFindings.Count > 0))
            blockers.Add("invalid_source_rows");
        if (ledger.GetState(batch) != "preview_ready")
            blockers.Add("batch_not_preview_ready");
        foreach (var row in rows)
        {
            var target = await reader.HydrateApplicationAsync(request.TenantId, row.ApplicationId, ct).ConfigureAwait(false);
            if (!target.IsCreated || target.IsRetired || target.Revision != row.ExpectedApplicationRevision ||
                target.CheckPendingImportChanges() is not null)
                blockers.Add("source_claim_target_changed");
        }
        if (!await ApplicationImportOmissionSources.UnchangedAsync(reader, request.TenantId, batch, ledger, ct).ConfigureAwait(false))
            return Result<ApplicationImportOmissionPreview>.Failure(ApplicationImportOmissionSources.Changed());
        return Result<ApplicationImportOmissionPreview>.Success(new(request.TenantId, batch.Id, batch.SourceKey!,
            batch.SourceNamespace!, batch.Coverage!, ledger.GetRevision(batch), ledger.CommittedStreamPosition,
            batch.ContentDigest!, Array.AsReadOnly(rows), Array.AsReadOnly(blockers.Distinct(StringComparer.Ordinal).ToArray())));
    }
}

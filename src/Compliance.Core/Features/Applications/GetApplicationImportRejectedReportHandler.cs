using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class GetApplicationImportRejectedReportHandler(IAggregateReader reader)
    : IRequestHandler<GetApplicationImportRejectedReport, ApplicationImportRejectedReport>
{
    public async ValueTask<Result<ApplicationImportRejectedReport>> HandleAsync(IRequestContext<GetApplicationImportRejectedReport> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var loaded = await ApplicationImportLifecycleRead.LoadAsync(reader, request.TenantId,
            request.BatchId, request.MinimumRevision, ct).ConfigureAwait(false);
        if (!loaded.IsSuccess)
            return Result<ApplicationImportRejectedReport>.Failure(loaded.Error);
        var (batch, ledger) = loaded.Value;
        var state = ledger.GetState(batch);
        var rejected = new List<ApplicationImportRejectedRow>();
        var batchFindings = new List<string>();
        var missing = ledger.GetMissingSourceClaims(batch);
        if (!missing.IsSuccess)
            return Result<ApplicationImportRejectedReport>.Failure(missing.Error);
        if (state == "preview_ready" && missing.Value.Count > 0)
        {
            batchFindings.Add("missing_source_retirement_unavailable");
            batchFindings.Add("retirement_impact_unavailable");
        }
        if (state == "preview_ready" && ledger.HasOtherAcceptingBatch(batch))
            batchFindings.Add("source_acceptance_in_progress");
        foreach (var row in batch.GetRows())
        {
            var findings = row.ValidationFindings.ToList();
            if (state == "failed")
                findings.Add("batch_execution_failed");
            else if (state == "canceled")
                findings.Add("batch_canceled");
            else if (state == "preview_ready" && findings.Count == 0)
            {
                var choice = ledger.GetCorrelation(batch.Id, row.RowId);
                var claim = ledger.GetSourceClaim(row.SourceRecordId!);
                if (choice is null && claim is null)
                    findings.Add("correlation_required");
                else if (claim is not null && choice is not null &&
                         (choice.ApplicationId != claim.Observation.ApplicationId || choice.Decision == "create_new"))
                    findings.Add("source_claim_conflict");
                else if (choice?.Decision != "create_new")
                {
                    var targetId = choice?.ApplicationId ?? claim!.Observation.ApplicationId;
                    var expectedRevision = choice?.ExpectedApplicationRevision ?? claim?.Observation.ExpectedApplicationRevision ?? 1;
                    var target = await reader.HydrateApplicationAsync(request.TenantId, targetId, ct).ConfigureAwait(false);
                    if (!target.IsCreated || target.Id != targetId || target.Stream.Realm != request.TenantId.ToString())
                        findings.Add("target_unavailable");
                    else if (target.IsRetired)
                        findings.Add("target_retired");
                    else if (target.Revision != expectedRevision)
                        findings.Add("target_revision_conflict");
                }
            }
            if (findings.Count > 0)
                rejected.Add(new(row.RowId, row.RowNumber, row.SourceRecordId, findings.AsReadOnly()));
        }
        var fence = await ApplicationImportLifecycleRead.CheckAsync(reader, batch, ledger, ct).ConfigureAwait(false);
        if (!fence.IsSuccess)
            return Result<ApplicationImportRejectedReport>.Failure(fence.Error);
        var failure = ledger.GetFailure(batch.Id);
        return Result<ApplicationImportRejectedReport>.Success(new(request.TenantId, batch.Id,
            ledger.GetRevision(batch), state, rejected.AsReadOnly(), batchFindings.AsReadOnly(), failure?.FailureCode, failure?.RowId));
    }
}

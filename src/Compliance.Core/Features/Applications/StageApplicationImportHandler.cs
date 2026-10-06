using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class StageApplicationImportHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<StageApplicationImport, ApplicationImportRegistration>
{
    public async ValueTask<Result<ApplicationImportRegistration>> HandleAsync(
        IRequestContext<StageApplicationImport> context, CancellationToken ct)
    {
        var request = context.Request;
        var (memberId, display) = ApplicationActor.From(context);
        var result = await executor.ExecuteAsync(new ImportBatch(request.TenantId, ImportBatch.BatchIdFor(request)),
            batch => AggregateOutcome.CommitOnSuccess(batch.Stage(request, memberId,
                display, clock.GetUtcNow())), context, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
            return result;
        var ledger = await reader.HydrateAsync(new ApplicationImportLedger(request.TenantId,
            request.SourceKey, request.SourceNamespace), ct).ConfigureAwait(false);
        return ledger.GetCanceledRevision(result.Value.BatchId) is { } revision
            ? Result<ApplicationImportRegistration>.Success(result.Value with { Revision = revision })
            : result;
    }
}

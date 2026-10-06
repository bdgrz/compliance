using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class CorrelateApplicationImportRowHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<CorrelateApplicationImportRow>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<CorrelateApplicationImportRow> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var (memberId, display) = ApplicationActor.From(context);
        var batch = await reader.HydrateAsync(new ImportBatch(request.TenantId, request.BatchId), ct)
            .ConfigureAwait(false);
        if (!batch.IsCreated || batch.SourceKey is not { } sourceKey || batch.SourceNamespace is not { } sourceNamespace)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The import batch was not found."));
        var application = request.Decision == "link_existing" && request.ApplicationId is { } applicationId
            ? await reader.HydrateAsync(new DeclaredApplication(request.TenantId, applicationId), ct).ConfigureAwait(false)
            : null;
        return await executor.ExecuteAsync(new ApplicationImportLedger(request.TenantId, sourceKey, sourceNamespace),
            ledger => CommandFailureRequestAdapter.ToOutcome(ledger.Correlate(batch, request, application,
                memberId, display, clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}

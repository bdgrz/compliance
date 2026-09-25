using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class StageApplicationImportHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<StageApplicationImport, ApplicationImportRegistration>
{
    public ValueTask<Result<ApplicationImportRegistration>> HandleAsync(
        IRequestContext<StageApplicationImport> context, CancellationToken ct)
    {
        var request = context.Request;
        var (memberId, display) = ApplicationActor.From(context);
        return executor.ExecuteAsync(new ImportBatch(request.TenantId, ImportBatch.BatchIdFor(request)),
            batch => AggregateOutcome.CommitOnSuccess(batch.Stage(request, memberId,
                display, clock.GetUtcNow())), context, ct);
    }
}

using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class GetApplicationImportHandler(ApplicationImportReadConsistency consistency)
    : IRequestHandler<GetApplicationImport, ApplicationImportView>
{
    public ValueTask<Result<ApplicationImportView>> HandleAsync(
        IRequestContext<GetApplicationImport> context, CancellationToken ct) =>
        consistency.GetFreshAsync(context.Request.TenantId, context.Request.BatchId,
            context.Request.MinimumRevision, ct);
}

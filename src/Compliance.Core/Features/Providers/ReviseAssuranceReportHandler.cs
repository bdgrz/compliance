using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

public sealed class ReviseAssuranceReportHandler(IAggregateExecutor executor, IAggregateReader reader,
    AssuranceReferences references, TimeProvider clock)
    : IRequestHandler<ReviseAssuranceReport, AssuranceReportRegistration>
{
    public async ValueTask<Result<AssuranceReportRegistration>> HandleAsync(
        IRequestContext<ReviseAssuranceReport> context, CancellationToken ct)
    {
        var request = context.Request;
        var source = await reader.HydrateAsync(new ProviderAssuranceRegister(request.TenantId), ct).ConfigureAwait(false);
        if (source.CheckReportRetry(request.ReportId, request.ProviderId, context.RequestId,
                request.ExpectedRevision, request.Content) is { } retry)
            return retry;
        var provider = await references.ProviderAsync(request.TenantId, request.ProviderId, ct).ConfigureAwait(false);
        if (!provider.IsSuccess)
            return Result<AssuranceReportRegistration>.Failure(provider.Error);
        var artifact = await references.ArtifactAsync(request.TenantId, request.Content.Citation, ct).ConfigureAwait(false);
        if (!artifact.IsSuccess)
            return Result<AssuranceReportRegistration>.Failure(artifact.Error);
        var actor = ProviderActor.From(context);
        return await executor.ExecuteAsync(new ProviderAssuranceRegister(request.TenantId), register =>
            AggregateOutcome.CommitOnSuccess(register.ReviseReport(request.ReportId, request.ProviderId,
                context.RequestId, request.ExpectedRevision, request.Content, provider.Value.Revision, actor,
                clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}

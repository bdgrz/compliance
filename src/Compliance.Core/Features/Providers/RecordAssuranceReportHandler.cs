using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

public sealed class RecordAssuranceReportHandler(IAggregateExecutor executor, IAggregateReader reader,
    AssuranceReferences references, TimeProvider clock)
    : IRequestHandler<RecordAssuranceReport, AssuranceReportRegistration>
{
    public async ValueTask<Result<AssuranceReportRegistration>> HandleAsync(
        IRequestContext<RecordAssuranceReport> context, CancellationToken ct)
    {
        var request = context.Request;
        var source = await reader.HydrateAsync(new ProviderAssuranceRegister(request.TenantId), ct).ConfigureAwait(false);
        if (source.CheckReportRetry(context.RequestId, request.ProviderId, context.RequestId, null, request.Content) is { } retry)
            return retry;
        var provider = await references.ProviderAsync(request.TenantId, request.ProviderId, ct).ConfigureAwait(false);
        if (!provider.IsSuccess)
            return Result<AssuranceReportRegistration>.Failure(provider.Error);
        var artifact = await references.ArtifactAsync(request.TenantId, request.Content.Citation, ct).ConfigureAwait(false);
        if (!artifact.IsSuccess)
            return Result<AssuranceReportRegistration>.Failure(artifact.Error);
        var actor = ProviderActor.From(context);
        return await executor.ExecuteAsync(new ProviderAssuranceRegister(request.TenantId), register =>
            AggregateOutcome.CommitOnSuccess(register.RecordReport(context.RequestId, request.ProviderId,
                context.RequestId, request.Content, provider.Value.Revision, actor, clock.GetUtcNow())), context, ct)
            .ConfigureAwait(false);
    }
}

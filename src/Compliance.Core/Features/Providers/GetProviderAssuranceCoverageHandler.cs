using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>Evaluates from the authoritative streams, so the result never lags a projection.</summary>
public sealed class GetProviderAssuranceCoverageHandler(IAggregateReader reader, AssuranceReferences references,
    TimeProvider clock) : IRequestHandler<GetProviderAssuranceCoverage, ProviderAssuranceCoverageView>
{
    public async ValueTask<Result<ProviderAssuranceCoverageView>> HandleAsync(
        IRequestContext<GetProviderAssuranceCoverage> context, CancellationToken ct)
    {
        var request = context.Request;
        var provider = await references.ProviderAsync(request.TenantId, request.ProviderId, ct).ConfigureAwait(false);
        if (!provider.IsSuccess)
            return Result<ProviderAssuranceCoverageView>.Failure(provider.Error);
        var source = await reader.HydrateAsync(new ProviderAssuranceRegister(request.TenantId), ct).ConfigureAwait(false);
        var asOf = request.AsOf ?? DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        return Result<ProviderAssuranceCoverageView>.Success(ProviderAssuranceCoverage.Evaluate(provider.Value,
            source.Reports(request.ProviderId), source.Reviews(request.ProviderId), asOf));
    }
}

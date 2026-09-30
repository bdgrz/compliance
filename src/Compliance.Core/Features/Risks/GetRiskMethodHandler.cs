using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>Reads the small per-program method stream directly, so reads never lag writes.</summary>
public sealed class GetRiskMethodHandler(IAggregateReader reader)
    : IRequestHandler<GetRiskMethod, RiskMethodVersionView>
{
    public async ValueTask<Result<RiskMethodVersionView>> HandleAsync(
        IRequestContext<GetRiskMethod> context, CancellationToken ct)
    {
        var method = await reader.HydrateAsync(new RiskMethod(context.Request.TenantId,
            context.Request.ProgramId), ct).ConfigureAwait(false);
        return Found(method.Current);
    }

    internal static Result<RiskMethodVersionView> Found(RiskMethodVersionView? version) =>
        version is null
            ? Result<RiskMethodVersionView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The risk method version was not found."))
            : Result<RiskMethodVersionView>.Success(version);
}

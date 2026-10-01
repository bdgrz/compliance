using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

public sealed class GetEffectivePolicyVersionHandler(IAggregateReader reader)
    : IRequestHandler<GetEffectivePolicyVersion, PolicyVersionView>
{
    public async ValueTask<Result<PolicyVersionView>> HandleAsync(
        IRequestContext<GetEffectivePolicyVersion> context, CancellationToken ct)
    {
        var request = context.Request;
        var policy = await PolicySource.ReadAsync(reader, request.TenantId, request.ProgramId,
            request.PolicyId, ct).ConfigureAwait(false);
        if (!policy.IsSuccess)
            return Result<PolicyVersionView>.Failure(policy.Error);
        return policy.Value.EffectiveVersion(request.EffectiveOn) is { } version
            ? Result<PolicyVersionView>.Success(version)
            : Result<PolicyVersionView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "No policy version was effective on that date."));
    }
}

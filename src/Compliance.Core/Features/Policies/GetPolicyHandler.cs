using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

/// <summary>Reads the authoritative policy stream, so the record never lags its commands.</summary>
public sealed class GetPolicyHandler(IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<GetPolicy, PolicyView>
{
    public async ValueTask<Result<PolicyView>> HandleAsync(IRequestContext<GetPolicy> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var policy = await PolicySource.ReadAsync(reader, request.TenantId, request.ProgramId,
            request.PolicyId, ct).ConfigureAwait(false);
        return policy.IsSuccess
            ? Result<PolicyView>.Success(policy.Value.ToView(PolicySource.Today(clock)))
            : Result<PolicyView>.Failure(policy.Error);
    }
}

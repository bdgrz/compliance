using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

public sealed class ListPolicyVersionsHandler(IAggregateReader reader)
    : IRequestHandler<ListPolicyVersions, Page<PolicyVersionView>>
{
    public async ValueTask<Result<Page<PolicyVersionView>>> HandleAsync(
        IRequestContext<ListPolicyVersions> context, CancellationToken ct)
    {
        var request = context.Request;
        var policy = await PolicySource.ReadAsync(reader, request.TenantId, request.ProgramId,
            request.PolicyId, ct).ConfigureAwait(false);
        return policy.IsSuccess
            ? ControlActivationSource.Paginate(policy.Value.ReadVersions(), request.Limit,
                request.Cursor, "policy versions")
            : Result<Page<PolicyVersionView>>.Failure(policy.Error);
    }
}

using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

/// <summary>Reads the authoritative policy stream for one program.</summary>
public static class PolicySource
{
    public static async ValueTask<Result<Policy>> ReadAsync(IAggregateReader reader,
        Uuid tenantId, Uuid programId, Uuid policyId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var policy = await reader.HydrateAsync(new Policy(tenantId, policyId), ct)
            .ConfigureAwait(false);
        return policy.IsVisible && policy.ProgramId == programId
            ? Result<Policy>.Success(policy)
            : Result<Policy>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The policy was not found."));
    }

    public static async ValueTask<SeparationOfDutiesWaiver?> WaiverAsync(
        IAggregateReader reader, Uuid tenantId, Uuid? waiverId, CancellationToken ct) =>
        waiverId is { } id
            ? await reader.HydrateAsync(new SeparationOfDutiesWaiver(tenantId, id), ct)
                .ConfigureAwait(false)
            : null;

    public static DateOnly Today(TimeProvider clock) =>
        DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    public static AggregateOutcome<PolicyRegistration> Registration(Policy policy,
        CommandFailure? failure) => CommandFailureRequestAdapter.ToOutcome(failure,
        new PolicyRegistration(policy.Id, policy.Identifier ?? string.Empty, policy.Revision));
}

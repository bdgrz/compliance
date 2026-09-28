using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class RecordSeparationOfDutiesWaiverHandler(IAggregateExecutor executor,
    ITenantMembershipDirectoryReader memberships, TimeProvider clock)
    : IRequestHandler<RecordSeparationOfDutiesWaiver, SeparationOfDutiesWaiverView>
{
    public async ValueTask<Result<SeparationOfDutiesWaiverView>> HandleAsync(
        IRequestContext<RecordSeparationOfDutiesWaiver> context, CancellationToken ct)
    {
        var request = context.Request;
        var beneficiary = await memberships.GetAsync(request.TenantId.ToString(),
            request.BeneficiaryUserId, ct).ConfigureAwait(false);
        if (beneficiary is null || beneficiary.Affiliation == "firm_staff")
            return Result<SeparationOfDutiesWaiverView>.Failure(new RequestError(
                RequestErrorKind.Validation, "The waiver beneficiary must be an active tenant member."));
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var requesterUserId))
            return Result<SeparationOfDutiesWaiverView>.Failure(new RequestError(
                RequestErrorKind.Unauthorized, "Waiver administration requires a Bdgrz user identity."));
        var requesterMemberId = RbacIds.Member(request.TenantId, requesterUserId);
        var now = clock.GetUtcNow();
        return await executor.ExecuteAsync(new SeparationOfDutiesWaiver(request.TenantId,
                context.RequestId), waiver =>
            {
                var failure = waiver.Record(request.Scope,
                    RbacIds.Member(request.TenantId, request.BeneficiaryUserId), requesterMemberId,
                    UserIdentityClaims.BdgrzDisplay(context.Actor, requesterUserId),
                    request.Rationale, now, request.ExpiresAt);
                return failure is null
                    ? AggregateOutcome.Commit(Result<SeparationOfDutiesWaiverView>.Success(
                        waiver.ToView(now)))
                    : AggregateOutcome.Discard(Result<SeparationOfDutiesWaiverView>.Failure(
                        new RequestError(failure.Code switch
                        {
                            CommandFailureCode.InvalidContent => RequestErrorKind.Validation,
                            CommandFailureCode.StateConflict => RequestErrorKind.Conflict,
                            _ => RequestErrorKind.Forbidden,
                        }, failure.Message!)));
            }, context, ct).ConfigureAwait(false);
    }
}

using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class RegisterFirmStaffHandler(IAggregateExecutor executor,
    IPlatformUserDirectoryReader users, TimeProvider clock)
    : IRequestHandler<RegisterFirmStaff, FirmStaffMemberView>
{
    public async ValueTask<Result<FirmStaffMemberView>> HandleAsync(
        IRequestContext<RegisterFirmStaff> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!await users.ExistsAsync(request.UserId, ct).ConfigureAwait(false))
            return Result<FirmStaffMemberView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The canonical platform user was not found; wait for directory recovery before registering staff."));
        return await executor.ExecuteAsync(new FirmStaffDirectory(), directory => AggregateOutcome.CommitOnSuccess(
            directory.Register(context.RequestId, request.StaffMemberId, request.UserId, request.Practice,
                request.SourceReference, request.ExpectedSequence, FirmStaffOperator.From(context), clock.GetUtcNow())),
            context, ct).ConfigureAwait(false);
    }
}

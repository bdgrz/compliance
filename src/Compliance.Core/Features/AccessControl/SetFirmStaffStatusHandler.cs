using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class SetFirmStaffStatusHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<SetFirmStaffStatus, FirmStaffMemberView>
{
    public ValueTask<Result<FirmStaffMemberView>> HandleAsync(
        IRequestContext<SetFirmStaffStatus> context, CancellationToken ct) => executor.ExecuteAsync(new FirmStaffDirectory(),
        directory => AggregateOutcome.CommitOnSuccess(directory.SetStatus(context.RequestId,
            context.Request.StaffMemberId, context.Request.IsActive, context.Request.Reason,
            context.Request.ExpectedSequence, FirmStaffOperator.From(context), clock.GetUtcNow())), context, ct);
}

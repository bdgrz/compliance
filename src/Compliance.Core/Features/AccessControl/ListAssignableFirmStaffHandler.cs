using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class ListAssignableFirmStaffHandler(IAggregateReader reader)
    : IRequestHandler<ListAssignableFirmStaff, FirmStaffDirectoryView>
{
    public async ValueTask<Result<FirmStaffDirectoryView>> HandleAsync(IRequestContext<ListAssignableFirmStaff> context, CancellationToken ct)
    {
        var directory = await reader.HydrateAsync(new FirmStaffDirectory(), ct).ConfigureAwait(false);
        return Result<FirmStaffDirectoryView>.Success(directory.View(activeOnly: true));
    }
}

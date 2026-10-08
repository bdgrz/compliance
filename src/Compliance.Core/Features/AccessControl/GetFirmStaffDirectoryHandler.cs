using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class GetFirmStaffDirectoryHandler(IAggregateReader reader)
    : IRequestHandler<GetFirmStaffDirectory, FirmStaffDirectoryView>
{
    public async ValueTask<Result<FirmStaffDirectoryView>> HandleAsync(IRequestContext<GetFirmStaffDirectory> context, CancellationToken ct)
    {
        var directory = await reader.HydrateAsync(new FirmStaffDirectory(), ct).ConfigureAwait(false);
        return Result<FirmStaffDirectoryView>.Success(directory.View());
    }
}

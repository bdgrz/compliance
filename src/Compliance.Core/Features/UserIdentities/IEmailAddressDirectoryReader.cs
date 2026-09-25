using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

public interface IEmailAddressDirectoryReader
{
    ValueTask<EmailAddressView?> GetAsync(string emailAddress, CancellationToken ct = default);
    ValueTask<Page<EmailAddressView>> ListAsync(Uuid userId, int? limit, string? cursor,
        CancellationToken ct = default);
}

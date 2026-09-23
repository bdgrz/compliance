using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

public sealed record PlatformUserDirectoryEntry(Uuid UserId);

static class PlatformUserDirectorySchema
{
    public static readonly KvDirectory<PlatformUserDirectoryEntry, Uuid> Directory = new(
        "platform-users-by-id",
        ComplianceCoreJsonContext.Default.PlatformUserDirectoryEntry,
        static user => user.UserId,
        static userId => [userId.ToString()],
        []);
}

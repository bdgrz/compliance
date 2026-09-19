using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

static class EmailAddressDirectorySchema
{
    public static readonly KvDirectoryIndex<EmailAddressView> ByUser = new(
        "by_user", 1, static address => [address.UserId.ToString(), address.EmailAddress]);

    public static readonly KvDirectory<EmailAddressView, string> Directory = new(
        "email-addresses",
        ComplianceCoreJsonContext.Default.EmailAddressView,
        static address => address.EmailAddress,
        static address => [address],
        [ByUser]);
}

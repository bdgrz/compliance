using System.Security.Cryptography;
using System.Text;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

static class PermissionProjectionKeys
{
    static readonly ReadOnlyMemory<byte> StateKey = "state"u8.ToArray();
    static readonly ReadOnlyMemory<byte> GrantsStart = "grant\0"u8.ToArray();
    static readonly ReadOnlyMemory<byte> GrantsEnd = "grant\u0001"u8.ToArray();

    public static ReadOnlyMemory<byte> State => StateKey;
    public static ReadOnlyMemory<byte> GrantRangeStart => GrantsStart;
    public static ReadOnlyMemory<byte> GrantRangeEnd => GrantsEnd;

    public static ReadOnlyMemory<byte> Grant(Uuid memberId, string permission)
    {
        var normalized = Permissions.Normalize(permission);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
        return Encoding.UTF8.GetBytes($"grant\0{memberId}\0{hash}");
    }
}

namespace Bdgrz.Compliance.Features.AccessControl;

static class AccessGrantProjectionKeys
{
    static readonly ReadOnlyMemory<byte> StateKey = "access_grants"u8.ToArray();

    public static ReadOnlyMemory<byte> State => StateKey;
    public const string Route = "kv://bdgrz/access-grants-v1/projection";
    public const string Projector = "AccessGrantsV1";
}

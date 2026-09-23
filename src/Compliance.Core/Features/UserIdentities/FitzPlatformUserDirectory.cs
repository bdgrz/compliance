using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

sealed class FitzPlatformUserDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/platform-user-directory/projection",
        "PlatformUserDirectory"),
      IPlatformUserDirectoryProjection, IPlatformUserDirectoryReader
{
    const string Realm = "bdgrz";

    public async ValueTask ApplyAsync(UserIdentityRegistered registered,
        CancellationToken ct = default)
    {
        if (await PlatformUserDirectorySchema.Directory.GetAsync(Transaction,
                registered.UserId, ct).ConfigureAwait(false) is null)
            await PlatformUserDirectorySchema.Directory.InsertAsync(Transaction,
                new PlatformUserDirectoryEntry(registered.UserId), ct).ConfigureAwait(false);
    }

    public async ValueTask<bool> ExistsAsync(Uuid userId, CancellationToken ct = default)
    {
        if (userId == Uuid.Empty)
            return false;
        await using var tx = await BeginReadAsync(Realm, ct).ConfigureAwait(false);
        return await PlatformUserDirectorySchema.Directory.GetAsync(tx, userId, ct)
            .ConfigureAwait(false) is not null;
    }
}

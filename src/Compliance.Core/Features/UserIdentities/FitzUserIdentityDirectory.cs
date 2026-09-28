using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

sealed class FitzUserIdentityDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/user-identity-directory/projection",
        "UserIdentityDirectory"),
      IUserIdentityDirectoryProjection,
      IUserIdentityDirectoryReader
{
    const string Realm = "bdgrz";
    static readonly EventStreamPattern Source = EventStreamPattern.ForPattern(Realm, "user-identities");

    public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(CancellationToken ct = default) =>
        base.LoadCheckpointAsync(new CheckpointIdentity("UserIdentityDirectory", Source), ct);

    public async ValueTask ApplyAsync(UserIdentityRegistered registered, CancellationToken ct = default)
    {
        var identityId = UserIdentity.GetIdentityId(registered.Provider, registered.Identifier);
        var existing = await UserIdentityDirectorySchema.Directory.GetAsync(Transaction,
            identityId, ct).ConfigureAwait(false);
        if (existing is null)
            await UserIdentityDirectorySchema.Directory.InsertAsync(Transaction,
                new UserIdentityDirectoryEntry(registered.UserId, identityId,
                    registered.Provider, IsRevoked: false), ct).ConfigureAwait(false);
        else if (existing.UserId != registered.UserId || existing.Provider != registered.Provider ||
                 existing.IsRevoked)
            throw new InvalidOperationException("The identity directory registration conflicts with its source.");
        await AdvanceRevisionAsync(ct).ConfigureAwait(false);
    }

    public ValueTask ApplyAsync(UserIdentityAuthenticated authenticated,
        CancellationToken ct = default)
    {
        _ = authenticated;
        return AdvanceRevisionAsync(ct);
    }

    public async ValueTask ApplyAsync(UserIdentityRevoked revoked, CancellationToken ct = default)
    {
        var existing = await UserIdentityDirectorySchema.Directory.GetAsync(Transaction,
            revoked.UserIdentityId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("A revoked identity must already be registered.");
        if (existing.UserId != revoked.UserId)
            throw new InvalidOperationException("The revoked identity directory owner does not match its source.");
        if (!existing.IsRevoked)
            await UserIdentityDirectorySchema.Directory.ReplaceAsync(Transaction, existing,
                existing with { IsRevoked = true }, ct).ConfigureAwait(false);
        await AdvanceRevisionAsync(ct).ConfigureAwait(false);
    }

    public async ValueTask<UserIdentityDirectoryPage> ListAsync(Uuid userId, int limit,
        string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(Realm, ct).ConfigureAwait(false);
        var revision = await UserIdentityDirectorySchema.Revisions.GetAsync(tx, "current", ct)
            .ConfigureAwait(false);
        var query = UserIdentityDirectorySchema.ByUser.Query()
            .WithPrefix(userId.ToString()).Take(limit).After(cursor);
        var identities = await UserIdentityDirectorySchema.Directory.QueryAsync(tx, query, ct)
            .ConfigureAwait(false);
        return new UserIdentityDirectoryPage(revision?.Revision ?? 0, identities);
    }

    async ValueTask AdvanceRevisionAsync(CancellationToken ct)
    {
        var current = await UserIdentityDirectorySchema.Revisions.GetAsync(Transaction, "current", ct)
            .ConfigureAwait(false);
        if (current is null)
            await UserIdentityDirectorySchema.Revisions.InsertAsync(Transaction,
                new UserIdentityDirectoryRevision("current", 1), ct).ConfigureAwait(false);
        else
            await UserIdentityDirectorySchema.Revisions.ReplaceAsync(Transaction, current,
                current with { Revision = checked(current.Revision + 1) }, ct).ConfigureAwait(false);
    }
}

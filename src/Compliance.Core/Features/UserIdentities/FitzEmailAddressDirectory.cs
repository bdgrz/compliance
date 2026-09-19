using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

sealed class FitzEmailAddressDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/email-address-directory/projection", "EmailAddressDirectory"),
      IEmailAddressDirectoryProjection,
      IEmailAddressDirectoryReader
{
    const string Realm = "bdgrz";

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case EmailAddressReserved reserved:
                await EmailAddressDirectorySchema.Directory.InsertAsync(
                    Transaction, new EmailAddressView(reserved.UserId, reserved.EmailAddress, false), ct)
                    .ConfigureAwait(false);
                break;
            case EmailAddressVerified verified:
                var previous = await EmailAddressDirectorySchema.Directory.GetAsync(
                    Transaction, verified.EmailAddress, ct).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("An email address must be reserved before verification.");
                await EmailAddressDirectorySchema.Directory.ReplaceAsync(
                    Transaction, previous, previous with { Verified = true }, ct).ConfigureAwait(false);
                break;
        }
    }

    public async ValueTask<EmailAddressView?> GetAsync(string emailAddress, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(Realm, ct).ConfigureAwait(false);
        return await EmailAddressDirectorySchema.Directory.GetAsync(tx, emailAddress, ct).ConfigureAwait(false);
    }

    public async ValueTask<Page<EmailAddressView>> ListAsync(Uuid userId, int? limit, string? cursor,
        CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(Realm, ct).ConfigureAwait(false);
        var query = EmailAddressDirectorySchema.ByUser.Query().WithPrefix(userId.ToString()).After(cursor);
        query = query.Take(limit ?? 50);
        return await EmailAddressDirectorySchema.Directory.QueryAsync(tx, query, ct).ConfigureAwait(false);
    }
}

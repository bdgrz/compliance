using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

sealed class FitzTenantInvitationDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/tenant-invitation-directory/projection",
        "TenantInvitationDirectory"),
      ITenantInvitationDirectoryReader, ITenantInvitationDirectoryProjection
{
    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case TenantMemberInvited invited:
                var next = new TenantInvitationDirectoryEntry(invited.TenantId,
                    invited.EmailAddress, invited.Affiliation, invited.Administrator,
                    invited.BuiltInRole, invited.ExpiresAt, invited.InvitedBy, null)
                {
                    DeliveryStatus = "pending",
                };
                var prior = await TenantInvitationDirectorySchema.Directory.GetAsync(Transaction,
                    invited.EmailAddress, ct).ConfigureAwait(false);
                if (prior is null)
                    await TenantInvitationDirectorySchema.Directory.InsertAsync(Transaction,
                        next, ct).ConfigureAwait(false);
                else
                    await TenantInvitationDirectorySchema.Directory.ReplaceAsync(Transaction,
                        prior, next, ct).ConfigureAwait(false);
                break;
            case TenantInvitationAccepted accepted:
                var current = await TenantInvitationDirectorySchema.Directory.GetAsync(Transaction,
                    accepted.EmailAddress, ct).ConfigureAwait(false);
                if (current is null || current.TenantId != accepted.TenantId)
                    throw new InvalidOperationException(
                        "An invitation cannot be accepted before it is projected.");
                await TenantInvitationDirectorySchema.Directory.ReplaceAsync(Transaction,
                    current, current with { AcceptedUserId = accepted.UserId }, ct)
                    .ConfigureAwait(false);
                break;
            case TenantInvitationDeliverySent sent:
                var delivered = await TenantInvitationDirectorySchema.Directory.GetAsync(
                    Transaction, sent.EmailAddress, ct).ConfigureAwait(false);
                if (delivered is null || delivered.TenantId != sent.TenantId)
                    throw new InvalidOperationException(
                        "A delivery outcome cannot be projected before its invitation.");
                await TenantInvitationDirectorySchema.Directory.ReplaceAsync(Transaction,
                    delivered, delivered with { DeliveryStatus = "delivered" }, ct)
                    .ConfigureAwait(false);
                break;
            case TenantInvitationDeliveryFailed failed:
                var failedInvitation = await TenantInvitationDirectorySchema.Directory.GetAsync(
                    Transaction, failed.EmailAddress, ct).ConfigureAwait(false);
                if (failedInvitation is null || failedInvitation.TenantId != failed.TenantId)
                    throw new InvalidOperationException(
                        "A delivery outcome cannot be projected before its invitation.");
                await TenantInvitationDirectorySchema.Directory.ReplaceAsync(Transaction,
                    failedInvitation, failedInvitation with { DeliveryStatus = "failed" }, ct)
                    .ConfigureAwait(false);
                break;
        }
    }

    public async ValueTask<TenantInvitationDirectoryEntry?> GetAsync(Uuid tenantId,
        string emailAddress, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await TenantInvitationDirectorySchema.Directory.GetAsync(tx, emailAddress, ct)
            .ConfigureAwait(false);
    }

    public async ValueTask<Page<TenantInvitationDirectoryEntry>> ListAsync(Uuid tenantId,
        int limit, string? cursor, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await TenantInvitationDirectorySchema.Directory.QueryAsync(tx,
            TenantInvitationDirectorySchema.ByEmail.Query().Take(Math.Clamp(limit, 1, 200))
                .After(cursor), ct).ConfigureAwait(false);
    }
}

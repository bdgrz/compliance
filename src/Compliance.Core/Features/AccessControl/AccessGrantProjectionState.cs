using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

sealed class AccessGrantProjectionState
{
    public Uuid TenantId { get; set; }
    public long Revision { get; set; }
    public List<AccessGrantView> Grants { get; init; } = [];
    public List<AccessGrantMembershipEpisode> MembershipEpisodes { get; init; } = [];

    public void Apply(DomainEvent domainEvent)
    {
        switch (domainEvent)
        {
            case AccessGrantIssued issued:
                EnsureTenant(issued.TenantId);
                var existingIndex = Grants.FindIndex(item => item.GrantId == issued.GrantId);
                if (existingIndex >= 0)
                {
                    var prior = Grants[existingIndex];
                    var priorEpisodeId = MembershipEpisodes.SingleOrDefault(item =>
                        item.GrantId == issued.GrantId)?.MembershipEpisodeId;
                    if (prior.TenantId == issued.TenantId && prior.Terms == issued.Terms &&
                        priorEpisodeId == issued.MembershipEpisodeId)
                        return;
                    throw new InvalidOperationException(
                        "An access grant issue event was replayed with different terms.");
                }
                Grants.Add(new AccessGrantView(issued.TenantId, issued.GrantId,
                    issued.Terms, null, null));
                MembershipEpisodes.Add(new AccessGrantMembershipEpisode(issued.GrantId,
                    issued.MembershipEpisodeId));
                Revision = checked(Revision + 1);
                break;
            case AccessGrantRevoked revoked:
                EnsureTenant(revoked.TenantId);
                var revokedIndex = Grants.FindIndex(item => item.GrantId == revoked.GrantId);
                if (revokedIndex < 0)
                    throw new InvalidOperationException(
                        "An access grant cannot be revoked before it is issued.");
                var current = Grants[revokedIndex];
                if (current.RevokedAt is { } priorRevokedAt)
                {
                    if (priorRevokedAt == revoked.RevokedAt && current.RevokedBy == revoked.RevokedBy)
                        return;
                    throw new InvalidOperationException(
                        "An access grant revocation event was replayed with different terms.");
                }
                Grants[revokedIndex] = current with
                {
                    RevokedBy = revoked.RevokedBy,
                    RevokedAt = revoked.RevokedAt,
                };
                Revision = checked(Revision + 1);
                break;
        }
    }

    public AccessGrantSetView ToView(Uuid tenantId)
    {
        if (TenantId != Uuid.Empty && TenantId != tenantId)
            throw new InvalidOperationException("An access grant projection cannot cross tenant boundaries.");
        return new AccessGrantSetView(tenantId, Revision, Grants
            .OrderBy(item => item.GrantId.ToString(), StringComparer.Ordinal).ToArray());
    }

    public Uuid? GetMembershipEpisodeId(Uuid grantId) =>
        MembershipEpisodes.SingleOrDefault(item => item.GrantId == grantId)?.MembershipEpisodeId;

    void EnsureTenant(Uuid tenantId)
    {
        if (tenantId == Uuid.Empty || TenantId != Uuid.Empty && TenantId != tenantId)
            throw new InvalidOperationException("An access grant projection cannot cross tenant boundaries.");
        TenantId = tenantId;
    }
}

using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class TenantSlug : Aggregate
{
    static Uuid SlugNamespaceId { get; } =
        Uuid.Parse("4f46ea79-15fd-5e46-b715-3e32e5899af8", CultureInfo.InvariantCulture);

    readonly string _slug;
    Uuid? _tenantId;
    bool _retired;

    public TenantSlug(string slug)
        : base(CreateId(slug), new EventStreamAddress("bdgrz", "tenant-slugs", CreateId(slug).ToString()))
    {
        _slug = Normalize(slug);
        On<TenantSlugRegistered>(Apply);
        On<TenantSlugRegistrationRejected>(_ => { });
        On<TenantSlugSurrendered>(Apply);
        On<TenantSlugSurrenderRejected>(_ => { });
    }

    /// <summary>
    ///     Reports whether <paramref name="tenantId" /> could claim this slug right now. A cheap,
    ///     possibly-stale preflight for <see cref="RegisterTenantSlugAvailabilityGuard" />; only
    ///     <see cref="Register" /> decides ownership authoritatively. A retired slug (once
    ///     surrendered) is never available again, even to the tenant that surrendered it.
    /// </summary>
    public bool IsAvailableFor(Uuid tenantId) => !_retired && (_tenantId is null || _tenantId == tenantId);

    public Result Register(Uuid tenantId)
    {
        if (!_retired && _tenantId is null)
            RaiseEvent(new TenantSlugRegistered(tenantId, _slug));
        else if (_tenantId != tenantId || _retired)
            RaiseEvent(new TenantSlugRegistrationRejected(tenantId, _slug));
        return Result.Success;
    }

    public Result Surrender(Uuid tenantId)
    {
        if (_tenantId == tenantId)
            RaiseEvent(new TenantSlugSurrendered(tenantId, _slug));
        else
            RaiseEvent(new TenantSlugSurrenderRejected(tenantId, _slug));
        return Result.Success;
    }

    void Apply(TenantSlugRegistered registered) => _tenantId = registered.TenantId;

    // A slug is permanently spent once surrendered, per the backlog's "a retired slug is never
    // assigned to another organization" rule -- retiring clears ownership but never un-retires.
    void Apply(TenantSlugSurrendered _)
    {
        _tenantId = null;
        _retired = true;
    }

    static Uuid CreateId(string slug) => Uuid.CreateVersion5(SlugNamespaceId, Normalize(slug));
    static string Normalize(string slug) => TenantSlugs.TryNormalize(slug, out var normalized)
        ? normalized
        : throw new ArgumentException("A valid tenant slug is required.", nameof(slug));
}

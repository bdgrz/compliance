using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class TenantSlug : Aggregate
{
    static Uuid SlugNamespaceId { get; } =
        Uuid.Parse("4f46ea79-15fd-5e46-b715-3e32e5899af8", CultureInfo.InvariantCulture);

    readonly string _slug;
    Uuid? _tenantId;

    public TenantSlug(string slug)
        : base(CreateId(slug), new EventStreamAddress("bdgrz", "tenant-slugs", CreateId(slug).ToString()))
    {
        _slug = Normalize(slug);
        On<TenantSlugRegistered>(Apply);
        On<TenantSlugRegistrationRejected>(_ => { });
        On<TenantSlugSurrendered>(Apply);
        On<TenantSlugSurrenderRejected>(_ => { });
    }

    public Result Register(Uuid tenantId)
    {
        if (_tenantId is null)
            RaiseEvent(new TenantSlugRegistered(tenantId, _slug));
        else if (_tenantId != tenantId)
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
    void Apply(TenantSlugSurrendered _) => _tenantId = null;

    static Uuid CreateId(string slug) => Uuid.CreateVersion5(SlugNamespaceId, Normalize(slug));
    static string Normalize(string slug) => TenantSlugs.TryNormalize(slug, out var normalized)
        ? normalized
        : throw new ArgumentException("A valid tenant slug is required.", nameof(slug));
}

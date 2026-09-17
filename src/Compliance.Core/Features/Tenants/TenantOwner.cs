using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class TenantOwner : Aggregate
{
    static Uuid OwnerNamespaceId { get; } =
        Uuid.Parse("aa628176-f556-5821-8cb0-b28b2319fe49", CultureInfo.InvariantCulture);

    readonly Uuid _tenantId;
    readonly Uuid _userId;
    bool _isRegistered;

    public TenantOwner(Uuid tenantId, Uuid userId)
        : base(
            CreateId(tenantId, userId),
            new EventStreamAddress("bdgrz", "tenant-owners", CreateId(tenantId, userId).ToString()))
    {
        _tenantId = tenantId;
        _userId = userId;
        On<TenantOwnerRegistered>(Apply);
    }

    public Result Register()
    {
        if (!_isRegistered)
            RaiseEvent(new TenantOwnerRegistered(_tenantId, _userId));
        return Result.Success;
    }

    void Apply(TenantOwnerRegistered _) => _isRegistered = true;

    static Uuid CreateId(Uuid tenantId, Uuid userId)
    {
        if (tenantId == Uuid.Empty || userId == Uuid.Empty)
            throw new ArgumentException("A tenant owner requires tenant and user IDs.");
        return Uuid.CreateVersion5(OwnerNamespaceId, $"{tenantId}\n{userId}");
    }
}

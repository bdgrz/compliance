using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
/// The facts a scope decision needs about one system instance, whether it was registered in its
/// own stream or declared in a legacy application stream.
/// </summary>
public sealed record ScopedSystemInstance(Uuid Id, Uuid ApplicationId, long Revision,
    bool IsRetired, Uuid RegisteredByMemberId)
{
    public static ScopedSystemInstance From(DeclaredSystemInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return new ScopedSystemInstance(instance.Id, instance.ApplicationId, instance.Revision,
            instance.IsRetired, instance.RegisteredByMemberId);
    }

    /// <summary>A legacy application-stream declaration has one immutable revision.</summary>
    public static ScopedSystemInstance FromLegacy(SystemInstanceDeclared declared)
    {
        ArgumentNullException.ThrowIfNull(declared);
        return new ScopedSystemInstance(declared.SystemInstanceId, declared.ApplicationId, 1,
            false, declared.ActorMemberId);
    }
}

using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.E2E;

sealed class TransientTenantAccessPermissionLag
{
    readonly TaskCompletionSource _firstFailure = new(TaskCreationOptions.RunContinuationsAsynchronously);
    int _failureCount;
    int _released;
    string? _targetUserId;

    internal int FailureCount => Volatile.Read(ref _failureCount);

    internal Task WaitForFirstFailureAsync(CancellationToken ct) => _firstFailure.Task.WaitAsync(ct);

    internal void TargetUser(string userId) => Interlocked.Exchange(ref _targetUserId, userId);

    internal bool ShouldFail(Uuid userId, string permission)
    {
        if (permission != RbacPermissions.TenantAccess ||
            !string.Equals(userId.ToString(), Volatile.Read(ref _targetUserId), StringComparison.Ordinal) ||
            Volatile.Read(ref _released) == 1)
            return false;

        _ = Interlocked.Increment(ref _failureCount);
        _firstFailure.TrySetResult();
        return true;
    }

    internal void Release() => Volatile.Write(ref _released, 1);
}

sealed class TransientTenantAccessPermissionAuthorizer(IPermissionAuthorizer inner,
    TransientTenantAccessPermissionLag lag) : IPermissionAuthorizer
{
    public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission,
        CancellationToken ct = default) => lag.ShouldFail(userId, permission)
        ? ValueTask.FromResult(false)
        : inner.IsAllowedAsync(tenantId, userId, memberId, permission, ct);
}

static class TransientTenantAccessPermissionTestRegistration
{
    internal static void Install(IServiceCollection services, TransientTenantAccessPermissionLag lag)
    {
        var registration = services.Last(descriptor => descriptor.ServiceType == typeof(IPermissionAuthorizer));
        if (registration.ImplementationFactory is not { } factory)
            throw new InvalidOperationException("The permission authorizer registration must use a factory.");

        _ = services.Remove(registration);
        services.AddSingleton(lag);
        services.AddScoped<IPermissionAuthorizer>(provider => new TransientTenantAccessPermissionAuthorizer(
            (IPermissionAuthorizer)factory(provider), lag));
    }
}

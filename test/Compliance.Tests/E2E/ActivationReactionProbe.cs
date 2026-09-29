using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Claims;
using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.E2E;

sealed class ActivationReactionProbe
{
    readonly ConcurrentQueue<string> _attempts = new();
    readonly long _started = Stopwatch.GetTimestamp();
    int _failedAttempts;
    int _successfulAttempts;
    string? _targetTenantId;

    internal IReadOnlyList<string> Attempts => _attempts.ToArray();
    internal int FailedAttempts => Volatile.Read(ref _failedAttempts);
    internal int SuccessfulAttempts => Volatile.Read(ref _successfulAttempts);

    internal void TargetTenant(Uuid tenantId) =>
        Interlocked.Exchange(ref _targetTenantId, tenantId.ToString());

    internal static void Install(IServiceCollection services, ActivationReactionProbe probe)
    {
        var registration = services.Last(descriptor => descriptor.ServiceType == typeof(IRequestBus));
        if (registration.ImplementationType is not { } implementationType)
            throw new InvalidOperationException("The request bus registration must use an implementation type.");

        _ = services.Remove(registration);
        services.AddSingleton(probe);
        services.AddScoped<IRequestBus>(provider => new RecordingBus(
            (IRequestBus)ActivatorUtilities.CreateInstance(provider, implementationType), probe));
    }

    void Record(Result result)
    {
        if (result.IsSuccess)
            _ = Interlocked.Increment(ref _successfulAttempts);
        else
            _ = Interlocked.Increment(ref _failedAttempts);
        var outcome = result.IsSuccess
            ? "success"
            : $"{result.Error.Kind}:transient={result.Error.IsTransient}:{result.Error.Message}";
        _attempts.Enqueue($"{Stopwatch.GetElapsedTime(_started).TotalMilliseconds:F0}ms={outcome}");
    }

    void Record(Exception exception) =>
        _attempts.Enqueue($"{Stopwatch.GetElapsedTime(_started).TotalMilliseconds:F0}ms={exception.GetType().Name}");

    sealed class RecordingBus(IRequestBus inner, ActivationReactionProbe probe) : IRequestBus
    {
        public RequestDispatchContext CreateContext(ClaimsPrincipal actor, RequestMetadata? metadata = null) =>
            inner.CreateContext(actor, metadata);

        public ValueTask<Result> AuthorizeAsync(IRequestBase request, RequestDispatchContext context,
            CancellationToken ct = default) => inner.AuthorizeAsync(request, context, ct);

        public async ValueTask<Result> DispatchAsync(IRequest request, RequestDispatchContext context,
            CancellationToken ct = default)
        {
            if (request is not ActivateTenant activation ||
                !string.Equals(activation.TenantId.ToString(), Volatile.Read(ref probe._targetTenantId),
                    StringComparison.Ordinal))
                return await inner.DispatchAsync(request, context, ct);

            try
            {
                var result = await inner.DispatchAsync(request, context, ct);
                probe.Record(result);
                return result;
            }
            catch (Exception exception)
            {
                probe.Record(exception);
                throw;
            }
        }

        public ValueTask<Result<TOut>> DispatchAsync<TOut>(IRequest<TOut> request,
            RequestDispatchContext context, CancellationToken ct = default) =>
            inner.DispatchAsync(request, context, ct);

        public IAsyncEnumerable<TOut> DispatchStreamAsync<TOut>(IStreamRequest<TOut> request,
            RequestDispatchContext context, CancellationToken ct = default) =>
            inner.DispatchStreamAsync(request, context, ct);
    }
}

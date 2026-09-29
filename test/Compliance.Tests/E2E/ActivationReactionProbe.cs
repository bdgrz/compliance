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
    readonly ConcurrentQueue<string> _preparationAttempts = new();
    readonly long _started = Stopwatch.GetTimestamp();
    int _failedAttempts;
    int _successfulAttempts;
    string? _targetTenantId;

    internal IReadOnlyList<string> Attempts => _attempts.ToArray();
    internal IReadOnlyList<string> PreparationAttempts => _preparationAttempts.ToArray();
    internal int FailedAttempts => Volatile.Read(ref _failedAttempts);
    internal int SuccessfulAttempts => Volatile.Read(ref _successfulAttempts);
    internal double ElapsedMilliseconds => Stopwatch.GetElapsedTime(_started).TotalMilliseconds;
    internal double ElapsedMillisecondsAt(long timestamp) =>
        Stopwatch.GetElapsedTime(_started, timestamp).TotalMilliseconds;

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

    void Record(Result result, long started)
    {
        if (result.IsSuccess)
            _ = Interlocked.Increment(ref _successfulAttempts);
        else
            _ = Interlocked.Increment(ref _failedAttempts);
        var outcome = result.IsSuccess
            ? "success"
            : $"{result.Error.Kind}:transient={result.Error.IsTransient}:{result.Error.Message}";
        var ended = Stopwatch.GetTimestamp();
        _attempts.Enqueue($"{ElapsedMillisecondsAt(started):F0}->{ElapsedMillisecondsAt(ended):F0}ms" +
            $"({Stopwatch.GetElapsedTime(started, ended).TotalMilliseconds:F0}ms)={outcome}");
    }

    void Record(Exception exception, long started)
    {
        var ended = Stopwatch.GetTimestamp();
        _attempts.Enqueue($"{ElapsedMillisecondsAt(started):F0}->{ElapsedMillisecondsAt(ended):F0}ms" +
            $"({Stopwatch.GetElapsedTime(started, ended).TotalMilliseconds:F0}ms)={exception.GetType().Name}");
    }

    void RecordPreparation(IRequest command, long started, string outcome)
    {
        var ended = Stopwatch.GetTimestamp();
        _preparationAttempts.Enqueue($"{command.GetType().Name}:" +
            $"{ElapsedMillisecondsAt(started):F0}->{ElapsedMillisecondsAt(ended):F0}ms" +
            $"({Stopwatch.GetElapsedTime(started, ended).TotalMilliseconds:F0}ms)={outcome}");
    }

    sealed class RecordingBus(IRequestBus inner, ActivationReactionProbe probe) : IRequestBus
    {
        public RequestDispatchContext CreateContext(ClaimsPrincipal actor, RequestMetadata? metadata = null) =>
            inner.CreateContext(actor, metadata);

        public ValueTask<Result> AuthorizeAsync(IRequestBase request, RequestDispatchContext context,
            CancellationToken ct = default) => inner.AuthorizeAsync(request, context, ct);

        public async ValueTask<Result> DispatchAsync(IRequest request, RequestDispatchContext context,
            CancellationToken ct = default)
        {
            var targetTenantId = Volatile.Read(ref probe._targetTenantId);
            var targetActivation = request is ActivateTenant activation &&
                string.Equals(activation.TenantId.ToString(), targetTenantId, StringComparison.Ordinal);
            var targetPreparation = request switch
            {
                RegisterMember member => string.Equals(member.TenantId.ToString(), targetTenantId,
                    StringComparison.Ordinal),
                AssignTeamMember assignment => string.Equals(assignment.TenantId.ToString(), targetTenantId,
                    StringComparison.Ordinal),
                _ => false,
            };
            if (!targetActivation && !targetPreparation)
                return await inner.DispatchAsync(request, context, ct);

            var started = Stopwatch.GetTimestamp();
            try
            {
                var result = await inner.DispatchAsync(request, context, ct);
                if (targetActivation)
                    probe.Record(result, started);
                else
                    probe.RecordPreparation(request, started,
                        result.IsSuccess ? "success" : result.Error.Kind.ToString());
                return result;
            }
            catch (Exception exception)
            {
                if (targetActivation)
                    probe.Record(exception, started);
                else
                    probe.RecordPreparation(request, started, exception.GetType().Name);
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

using System.Collections.Concurrent;
using System.Diagnostics;
using Cntryl.Portia;
using Microsoft.Extensions.Logging;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
///     The failure policy of the per-tenant activation reactors (#430). A transient failure, such
///     as a permission projection that has not caught up, is retried locally for
///     <see cref="LocalRetryWindow" /> and then returned to Portia's durable replay with the
///     checkpoint unchanged. Those workloads register <see cref="WorkloadFailureAttemptLimit" />,
///     so a prolonged outage for one tenant never exhausts a failed-pass limit and never stops the
///     worker host or other tenants' workloads. A permanent (non-transient) failure is replayed
///     up to <see cref="PermanentFailureLimit" /> consecutive times in this process and then
///     abandoned: the reactor logs <c>LogActivationAbandoned</c> at error level and advances its
///     checkpoint, leaving the tenant inactive (fail closed) until an operator issues a new
///     administrator invitation.
/// </summary>
public sealed partial class TenantActivationPolicy
{
    /// <summary>Activation workloads never fault the host for consecutive failed passes.</summary>
    public const int WorkloadFailureAttemptLimit = int.MaxValue;

    readonly ConcurrentDictionary<(string Reactor, Uuid EventId), int> _permanentFailures = new();

    public TenantActivationPolicy(TimeSpan? localRetryWindow = null, int permanentFailureLimit = 20)
    {
        LocalRetryWindow = localRetryWindow ?? TimeSpan.FromSeconds(8);
        ArgumentOutOfRangeException.ThrowIfLessThan(LocalRetryWindow, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(permanentFailureLimit, 1);
        PermanentFailureLimit = permanentFailureLimit;
    }

    internal static TenantActivationPolicy Default { get; } = new();

    public TimeSpan LocalRetryWindow { get; }
    public int PermanentFailureLimit { get; }

    /// <summary>
    ///     Runs one activation reaction under the policy. A transient failure propagates for
    ///     durable replay; a permanent failure propagates until its budget is spent.
    /// </summary>
    public async ValueTask ReactAsync(string reactor, Uuid tenantId, DomainEvent trigger,
        ILogger logger, Func<CancellationToken, ValueTask> reaction, CancellationToken ct)
    {
        var key = (reactor, trigger.Metadata.EventId);
        try
        {
            await reaction(ct).ConfigureAwait(false);
            _ = _permanentFailures.TryRemove(key, out _);
        }
        catch (ReactionCommandFailedException failure) when (!failure.Error.IsTransient)
        {
            var attempts = _permanentFailures.AddOrUpdate(key, 1, static (_, count) => count + 1);
            if (attempts < PermanentFailureLimit)
                throw;
            _ = _permanentFailures.TryRemove(key, out _);
            LogActivationAbandoned(logger, tenantId, reactor, attempts, failure.Error.Kind,
                failure.Error.Message);
        }
    }

    /// <summary>Sends the activation, retrying a transient failure within the local window.</summary>
    public async ValueTask SendActivationAsync(IRequestBus bus, ActivateTenant activation,
        IReactorContext context, ILogger logger, CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        var attempts = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await bus.SendReactionAsync(activation, context, ct).ConfigureAwait(false);
                if (attempts > 0 && logger.IsEnabled(LogLevel.Information))
                {
                    var elapsedMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                    LogActivationRecovered(logger, activation.TenantId, attempts + 1,
                        elapsedMilliseconds);
                }
                return;
            }
            catch (ReactionCommandFailedException failure) when (failure.Error.IsTransient)
            {
                attempts++;
                if (attempts == 1)
                    LogActivationWaiting(logger, activation.TenantId, failure.Error.Kind);
                if (Stopwatch.GetElapsedTime(started) >= LocalRetryWindow)
                {
                    LogActivationDeferred(logger, activation.TenantId, attempts,
                        failure.Error.Kind);
                    throw;
                }

                // Projection materialization can lag the source reaction. An extended lag
                // returns to Portia's durable pass retry instead of holding this pass forever.
                await Task.Delay(TimeSpan.FromMilliseconds(250), ct).ConfigureAwait(false);
            }
        }
    }

    [LoggerMessage(EventId = 41501, Level = LogLevel.Information,
        Message = "Tenant {TenantId} activation is waiting for provisioning after {ErrorKind}.")]
    static partial void LogActivationWaiting(ILogger logger, Uuid tenantId, RequestErrorKind errorKind);

    [LoggerMessage(EventId = 41502, Level = LogLevel.Warning,
        Message = "Tenant {TenantId} activation remains pending after {Attempts} attempts and {ErrorKind}; the reaction will replay.")]
    static partial void LogActivationDeferred(ILogger logger, Uuid tenantId, int attempts,
        RequestErrorKind errorKind);

    [LoggerMessage(EventId = 41503, Level = LogLevel.Information,
        Message = "Tenant {TenantId} activation recovered on attempt {Attempt} after {ElapsedMilliseconds} ms.")]
    static partial void LogActivationRecovered(ILogger logger, Uuid tenantId, int attempt,
        double elapsedMilliseconds);

    [LoggerMessage(EventId = 41504, Level = LogLevel.Error,
        Message = "Tenant {TenantId} activation by {Reactor} was abandoned after {Attempts} permanent failures ({ErrorKind}: {Reason}); the tenant stays inactive until an operator issues a new administrator invitation.")]
    static partial void LogActivationAbandoned(ILogger logger, Uuid tenantId, string reactor,
        int attempts, RequestErrorKind errorKind, string reason);
}

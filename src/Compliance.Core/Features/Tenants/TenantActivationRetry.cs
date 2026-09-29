using System.Diagnostics;
using Cntryl.Portia;
using Microsoft.Extensions.Logging;

namespace Bdgrz.Compliance.Features.Tenants;

static partial class TenantActivationRetry
{
    public static async ValueTask SendAsync(IRequestBus bus, ActivateTenant activation,
        IReactorContext context, ILogger logger, CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        var attempts = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await bus.SendReactionAsync(activation, context, ct);
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
                if (Stopwatch.GetElapsedTime(started) >= TimeSpan.FromSeconds(8))
                {
                    LogActivationDeferred(logger, activation.TenantId, attempts,
                        failure.Error.Kind);
                    throw;
                }

                // Projection materialization can lag the source reaction. An extended lag
                // returns to Portia's durable pass retry instead of holding this pass forever.
                await Task.Delay(TimeSpan.FromMilliseconds(250), ct);
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
}

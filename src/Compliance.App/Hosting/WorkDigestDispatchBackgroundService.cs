using Bdgrz.Compliance.Features.Work;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bdgrz.Compliance.Hosting;

sealed partial class WorkDigestDispatchBackgroundService(IServiceScopeFactory scopes,
    WorkDigestDeliverySettings settings, ILogger<WorkDigestDispatchBackgroundService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!settings.IsReady)
            return;

        using var timer = new PeriodicTimer(settings.PollInterval);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<WorkDigestDispatchSweep>()
                    .RunOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception)
            {
                // Exceptions may carry provider or source details; report only the safe outcome.
                LogSweepFailed(logger);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    [LoggerMessage(EventId = 41511, Level = LogLevel.Error,
        Message = "Weekly digest dispatch sweep failed; the next poll will retry.")]
    static partial void LogSweepFailed(ILogger logger);
}

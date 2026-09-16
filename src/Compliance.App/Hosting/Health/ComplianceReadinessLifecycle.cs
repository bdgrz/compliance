namespace Bdgrz.Compliance.Hosting.Health;

sealed class ComplianceReadinessLifecycle(ComplianceReadiness readiness) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        readiness.IsReady = true;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        readiness.IsReady = false;
        return Task.CompletedTask;
    }
}

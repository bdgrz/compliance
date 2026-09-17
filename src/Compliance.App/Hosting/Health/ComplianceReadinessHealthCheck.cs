using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Bdgrz.Compliance.Hosting.Health;

sealed class ComplianceReadinessHealthCheck(ComplianceReadiness readiness) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(
            readiness.IsReady
                ? HealthCheckResult.Healthy("Application startup completed.")
                : HealthCheckResult.Unhealthy("Application startup is incomplete."));
}

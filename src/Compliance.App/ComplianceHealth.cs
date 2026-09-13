using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Bdgrz.Compliance;

sealed class ComplianceReadiness
{
    public bool IsReady { get; set; }
}

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

static class ComplianceHealthExtensions
{
    public static IServiceCollection AddComplianceHealthChecks(this IServiceCollection services)
    {
        services.AddSingleton<ComplianceReadiness>();
        services.AddHealthChecks()
            .AddCheck<ComplianceReadinessHealthCheck>("startup", tags: ["ready"]);
        services.AddHostedService<ComplianceReadinessLifecycle>();
        return services;
    }

    public static IEndpointRouteBuilder MapComplianceHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks(
                "/health/live",
                new HealthCheckOptions { Predicate = static _ => false })
            .AllowAnonymous();
        endpoints.MapHealthChecks(
                "/health/ready",
                new HealthCheckOptions { Predicate = static check => check.Tags.Contains("ready") })
            .AllowAnonymous();
        endpoints.MapHealthChecks(
                "/healthz",
                new HealthCheckOptions { Predicate = static check => check.Tags.Contains("ready") })
            .AllowAnonymous();
        return endpoints;
    }
}

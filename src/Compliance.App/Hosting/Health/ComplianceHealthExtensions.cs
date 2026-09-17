using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Bdgrz.Compliance.Hosting.Health;

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

using System.Diagnostics.Metrics;
using System.Globalization;
using System.Security.Claims;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Hosting;

/// <summary>
///     Writes one structured security record for each request-authorizer or permission denial made
///     while this API host serves an HTTP or MCP call.
/// </summary>
/// <remarks>
///     Portia reports each authorization policy outcome through its versioned
///     <c>portia.authorization.duration</c> instrument, synchronously on the dispatching call. This
///     listener reads the outcome tags there and adds only opaque identifiers from the ambient request:
///     the route template (never its values), the parsed <c>tenant_id</c> route value, and the Bdgrz
///     subject. Emails, slugs, query strings, and bodies are never read, so a denial cannot disclose
///     client data or confirm a tenant to an outsider beyond the not-found response already returned.
/// </remarks>
sealed partial class AuthorizationDenialLog(
    IHttpContextAccessor httpContexts,
    ILoggerFactory loggers) : IHostedService, IDisposable
{
    /// <summary>The path the Portia MCP transport is mapped at.</summary>
    public const string McpPath = "/mcp";

    const string AuthorizationInstrument = "portia.authorization.duration";

    readonly ILogger _logger = loggers.CreateLogger("Bdgrz.Compliance.Authorization");
    MeterListener? _listener;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == PortiaTelemetry.SourceName &&
                    instrument.Name == AuthorizationInstrument)
                    meterListener.EnableMeasurementEvents(instrument);
            },
        };
        _listener.SetMeasurementEventCallback<double>(OnAuthorizationMeasured);
        _listener.Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _listener?.Dispose();
        _listener = null;
    }

    void OnAuthorizationMeasured(Instrument instrument, double measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        string? outcome = null, component = null, stage = null;
        foreach (var tag in tags)
        {
            switch (tag.Key)
            {
                case "portia.outcome":
                    outcome = tag.Value as string;
                    break;
                case "portia.component.name":
                    component = tag.Value as string;
                    break;
                case "portia.stage":
                    stage = tag.Value as string;
                    break;
            }
        }

        if (outcome is not ("unauthorized" or "forbidden" or "not_found"))
            return;

        // This runs inside Portia's authorization call; a failed observation must never change or
        // replace the authorization result the caller receives.
        try
        {
            // The listener is process-wide; only the host that owns the ambient call records it.
            var httpContext = httpContexts.HttpContext;
            if (httpContext is null ||
                !ReferenceEquals(httpContext.RequestServices.GetService<AuthorizationDenialLog>(), this))
                return;

            var route = (httpContext.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
            LogDenied(_logger, outcome, stage ?? "unknown", component ?? "unknown", Transport(route),
                httpContext.Request.Method, route, TenantId(httpContext), ActorId(httpContext.User),
                httpContext.TraceIdentifier);
        }
#pragma warning disable CA1031 // Observation failures are swallowed so authorization stays authoritative.
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }

    static string Transport(string? route) =>
        route is not null && (route == McpPath || route.StartsWith(McpPath + "/", StringComparison.Ordinal))
            ? "mcp"
            : "http";

    static string? TenantId(HttpContext httpContext) =>
        httpContext.Request.RouteValues.TryGetValue("tenant_id", out var value) &&
        Uuid.TryParse(value as string, CultureInfo.InvariantCulture, out var tenantId)
            ? tenantId.ToString()
            : null;

    // Uses the request authorizers' own actor rule: only the Bdgrz session subject names the actor.
    static string? ActorId(ClaimsPrincipal user) =>
        UserIdentityClaims.TryGetBdgrzSubject(user, out var userId) ? userId.ToString() : null;

    [LoggerMessage(EventId = 4031, EventName = "AuthorizationDenied", Level = LogLevel.Warning,
        Message = "Authorization denied: {outcome} by {component} ({stage}) for {transport} {method} {route} " +
            "tenant {tenant_id} actor {actor_id} trace {trace_id}")]
    static partial void LogDenied(ILogger logger, string outcome, string stage, string component,
        string transport, string method, string? route, string? tenant_id, string? actor_id, string trace_id);
}

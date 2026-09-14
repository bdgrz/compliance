using System.Text.Json;
using Bdgrz.Compliance;
using Cntryl.Portia;

var hostMode = ComplianceHostModeParser.Parse(
    Environment.GetEnvironmentVariable("COMPLIANCE_HOST_MODE"));

if (hostMode.RunsApi())
{
    await RunApiAsync(args, hostMode);
}
else
{
    await RunWorkerAsync(args);
}

static async Task RunApiAsync(string[] args, ComplianceHostMode hostMode)
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddProblemDetails(options =>
    {
        options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["trace_id"] = context.HttpContext.TraceIdentifier;
    });
    var authentication = builder.Services.AddComplianceAuthentication(
        builder.Configuration,
        builder.Environment);
    var developerAuthentication = authentication is null;
    var portia = builder.Services.AddCompliance(builder.Configuration, developerAuthentication);
    // AddPortia returns the same builder and lets this host contribute its generated JSON context.
    var httpPortia = builder.Services.AddPortia().AddHttp();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<RegistrationSessionCookie>();
    if (developerAuthentication)
    {
        _ = httpPortia.AddRequestPipelineBehavior<DeveloperRegistrationSessionBehavior>(order: 1000);
    }
    else
    {
        _ = httpPortia.AddRequestPipelineBehavior<OidcRegistrationSessionBehavior>(order: 1000);
    }
    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.SerializerOptions.TypeInfoResolverChain.Insert(0, ComplianceJsonContext.Default);
    });

    if (hostMode.RunsWorkers())
    {
        portia.AddWorkers();
    }

    builder.Services.AddComplianceHealthChecks();

    var app = builder.Build();

    app.UseExceptionHandler();
    app.UseStatusCodePages();
    app.Use(async (context, next) =>
    {
        if ((HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)) &&
            !Path.HasExtension(context.Request.Path.Value) &&
            !context.Request.Path.StartsWithSegments("/api") &&
            context.Request.Path != "/auth/config" &&
            context.Request.Path != "/auth/session" &&
            !context.Request.Path.StartsWithSegments("/health") &&
            !context.Request.Path.StartsWithSegments("/openapi"))
        {
            context.Request.Path = "/index.html";
        }

        await next(context);
    });
    app.UseRouting();
    app.MapStaticAssets().AllowAnonymous();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapPortiaOpenApi();

    app.MapComplianceHealthChecks();
    app.MapGet(
            "/auth/config",
            () => authentication?.ToClientConfiguration() ??
                ComplianceAuthenticationClientConfiguration.Development)
        .AllowAnonymous()
        .ExcludeFromDescription();
    app.MapGet(
            "/auth/session",
            (HttpContext context) => Results.Ok(new BrowserSession(
                context.User.FindFirst("sub")?.Value ?? string.Empty,
                context.User.FindFirst("email")?.Value ?? string.Empty,
                string.Equals(context.User.FindFirst("email_verified")?.Value, "true", StringComparison.Ordinal))))
        .RequireAuthorization()
        .ExcludeFromDescription();

    if (developerAuthentication)
    {
        app.MapPortiaPost<RegisterDeveloperUser, RegisteredUserIdentity>("/api/v1/developer-user-identities")
            .AllowAnonymous()
            .WithTags("Users");
    }
    else
    {
        app.MapPortiaPost<RegisterOidcUser, RegisteredUserIdentity>("/api/v1/oidc-user-identities")
            .RequireAuthorization(ComplianceAuthorizationPolicies.OidcRegistration)
            .WithTags("Users");
    }
    app.MapMethods(
        "/api/{**path}",
        ["DELETE", "GET", "HEAD", "OPTIONS", "PATCH", "POST", "PUT"],
        () => Results.NotFound())
        .RequireAuthorization();

    await app.RunAsync();
}

static async Task RunWorkerAsync(string[] args)
{
    var builder = Host.CreateApplicationBuilder(args);
    var developerAuthentication = builder.Configuration.GetValue("BDGRZ_DEVELOPER_AUTH", false) ||
        string.Equals(
            builder.Configuration["Compliance:Authentication:Mode"],
            "Development",
            StringComparison.OrdinalIgnoreCase);
    if (developerAuthentication && !builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "BDGRZ_DEVELOPER_AUTH is only available in the Development environment.");
    }

    builder.Services
        .AddCompliance(builder.Configuration, developerAuthentication)
        .AddWorkers();
    builder.Services.AddComplianceHealthChecks();

    await builder.Build().RunAsync();
}

sealed record BrowserSession(string Id, string? EmailAddress, bool EmailAddressVerified);

/// <summary>
/// Exposes the application entry point to integration tests.
/// </summary>
public partial class Program
{
}

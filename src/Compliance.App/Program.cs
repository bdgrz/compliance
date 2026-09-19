using System.Globalization;
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
    builder.Services.AddPortia()
        .AddHttp()
        .AddMcpTool<DefineTeam>(tool => tool.Idempotent())
        .AddMcpTool<DeleteTeam>(tool => tool.Destructive())
        .AddMcpTool<GetTeam>(tool => tool.ReadOnly())
        .AddMcpTool<ListTeams>(tool => tool.ReadOnly())
        .AddMcpTool<AssignTeamMember>(tool => tool.Idempotent())
        .AddMcpTool<RemoveTeamMember>(tool => tool.Destructive())
        .AddMcpTool<ListTeamMembers>(tool => tool.ReadOnly())
        .AddMcpTool<DefineRole>(tool => tool.Idempotent())
        .AddMcpTool<DeleteRole>(tool => tool.Destructive())
        .AddMcpTool<GetRole>(tool => tool.ReadOnly())
        .AddMcpTool<ListRoles>(tool => tool.ReadOnly())
        .AddMcpTool<AssignRolePermission>(tool => tool.Idempotent())
        .AddMcpTool<RemoveRolePermission>(tool => tool.Destructive())
        .AddMcpTool<ListRolePermissions>(tool => tool.ReadOnly())
        .AddMcpTool<AssignTeamRole>(tool => tool.Idempotent())
        .AddMcpTool<RemoveTeamRole>(tool => tool.Destructive())
        .AddMcpTool<ListRoleTeams>(tool => tool.ReadOnly())
        .AddMcpTool<ListMyTenants>(tool => tool.ReadOnly())
        .AddMcpTool<RegisterTenant>()
        .AddMcpTool<SuspendTenant>(tool => tool.Destructive())
        .AddMcpTool<ReactivateTenant>(tool => tool.Idempotent())
        .AddMcpTool<InviteTenantMember>()
        .AddMcpTool<GetTenant>(tool => tool.ReadOnly())
        .AddMcpTool<ListTenantMembers>(tool => tool.ReadOnly())
        .AddMcpTool<ChangeTenantSlug>()
        .AddMcpTool<ResolveMyTenantSlug>(tool => tool.ReadOnly())
        .AddMcpTool<CreateProgram>()
        .AddMcpTool<ReviseProgram>(tool => tool.Idempotent())
        .AddMcpTool<GetProgram>(tool => tool.ReadOnly())
        .AddMcpTool<ListPrograms>(tool => tool.ReadOnly())
        .AddMcpHttp();
    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.SerializerOptions.TypeInfoResolverChain.Insert(0, ComplianceJsonContext.Default);
    });

    if (hostMode.RunsWorkers())
    {
        portia.AddWorkers();
    }

    builder.Services.AddHostedService<ReservedTenantRouteCollisionCheck>();
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
    app.MapPortiaMcp("/mcp").RequireAuthorization();

    app.MapComplianceHealthChecks();
    app.MapGet(
            "/auth/config",
            () => authentication?.ToClientConfiguration() ??
                ComplianceAuthenticationClientConfiguration.Development)
        .AllowAnonymous()
        .ExcludeFromDescription();
    app.MapGet(
            "/auth/session",
            async (HttpContext context, IEmailAddressDirectoryReader directory, CancellationToken ct) =>
            {
                var id = context.User.FindFirst("sub")?.Value ?? string.Empty;
                var email = context.User.FindFirst("email")?.Value ?? string.Empty;
                var verified = false;
                if (email.Length > 0 && Uuid.TryParse(id, CultureInfo.InvariantCulture, out var userId))
                {
                    var address = await directory.GetAsync(email, ct).ConfigureAwait(false);
                    verified = address?.UserId == userId && address.Verified;
                }

                return Results.Ok(new BrowserSession(id, email, verified));
            })
        .RequireAuthorization()
        .ExcludeFromDescription();
    app.MapPost(
            "/auth/logout",
            (HttpContext context) =>
            {
                UserSessionCookie.Clear(context);
                return Results.NoContent();
            })
        .RequireAuthorization(ComplianceAuthorizationPolicies.Session)
        .ExcludeFromDescription();

    var sessionTokens = app.Services.GetRequiredService<BdgrzSessionTokens>();
    if (developerAuthentication)
    {
        app.MapPortiaPost<ContinueWithDeveloperIdentity, AuthenticatedUserIdentity>(
                "/api/v1/developer-user-sessions",
                config => config.OnResult((http, result) => UserSessionCookie.Issue(http, result, sessionTokens)))
            .AllowAnonymous()
            .WithTags("Users");
    }
    else
    {
        app.MapPortiaPost<ContinueWithOidcProvider, AuthenticatedUserIdentity>(
                "/api/v1/oidc-user-sessions",
                config => config.OnResult((http, result) => UserSessionCookie.Issue(http, result, sessionTokens)))
            .RequireAuthorization(ComplianceAuthorizationPolicies.OidcContinuation)
            .WithTags("Users");
    }
    app.MapPortiaPost<RegisterTenant, TenantRegistration>("/api/v1/tenants")
        .RequireAuthorization()
        .WithTags("Tenants");
    app.MapPortiaPost<SuspendTenant>("/api/v1/tenants/{tenantId}/suspensions")
        .RequireAuthorization()
        .WithTags("Tenants");
    app.MapPortiaDelete<ReactivateTenant>("/api/v1/tenants/{tenantId}/suspensions")
        .RequireAuthorization()
        .WithTags("Tenants");
    app.MapPortiaPost<InviteTenantMember>("/api/v1/tenants/{tenantId}/invitations")
        .RequireAuthorization()
        .WithTags("Tenants");
    app.MapPortiaPost<AcceptTenantInvitation>("/api/v1/tenants/{tenantId}/invitations/acceptance")
        .RequireAuthorization()
        .WithTags("Tenants");
    app.MapPortiaGet<GetTenant, TenantView>("/api/v1/tenants/{tenantId}")
        .RequireAuthorization()
        .WithTags("Tenants");
    app.MapPortiaGet<ListTenantMembers, Page<TenantMembershipView>>("/api/v1/tenants/{tenantId}/members")
        .RequireAuthorization()
        .WithTags("Tenants");
    app.MapPortiaPost<ChangeTenantSlug>("/api/v1/tenants/{tenantId}/slug-changes")
        .RequireAuthorization()
        .WithTags("Tenants");
    app.MapPortiaGet<ResolveMyTenantSlug, TenantSlugResolution>("/api/v1/tenant-slugs/{slug}/mine")
        .RequireAuthorization()
        .WithTags("Tenants");
    app.MapPortiaPost<CreateProgram, ProgramRegistration>("/api/v1/tenants/{tenantId}/programs")
        .RequireAuthorization()
        .WithTags("Programs");
    app.MapPortiaPut<ReviseProgram>("/api/v1/tenants/{tenantId}/programs/{programId}")
        .RequireAuthorization()
        .WithTags("Programs");
    app.MapPortiaGet<GetProgram, ProgramView>("/api/v1/tenants/{tenantId}/programs/{programId}")
        .RequireAuthorization()
        .WithTags("Programs");
    app.MapPortiaGet<ListPrograms, Page<ProgramView>>("/api/v1/tenants/{tenantId}/programs")
        .RequireAuthorization()
        .WithTags("Programs");
    app.MapPortiaPost<ReserveEmail>("/api/v1/users/{userId}/email-addresses/{emailAddress}")
        .RequireAuthorization()
        .WithTags("Email addresses");
    app.MapPortiaPost<IssueEmailChallenge>("/api/v1/users/{userId}/email-addresses/{emailAddress}/challenges")
        .RequireAuthorization()
        .WithTags("Email addresses");
    app.MapPortiaPost<CompleteEmailChallenge>("/api/v1/users/{userId}/email-addresses/{emailAddress}/verifications")
        .RequireAuthorization()
        .WithTags("Email addresses");
    app.MapPortiaGet<GetEmailAddress, EmailAddressView>("/api/v1/users/{userId}/email-addresses/{emailAddress}")
        .RequireAuthorization()
        .WithTags("Email addresses");
    app.MapPortiaGet<ListEmailAddresses, Page<EmailAddressView>>("/api/v1/users/{userId}/email-addresses")
        .RequireAuthorization()
        .WithTags("Email addresses");
    app.MapPortiaGet<ListMyTenants, Page<TenantMembershipSummary>>("/api/v1/tenants/mine")
        .RequireAuthorization()
        .WithTags("Tenants");
    app.MapPortiaPost<DefineTeam>("/api/v1/tenants/{tenantId}/teams/{teamId}")
        .RequireAuthorization()
        .WithTags("Teams");
    app.MapPortiaDelete<DeleteTeam>("/api/v1/tenants/{tenantId}/teams/{teamId}")
        .RequireAuthorization()
        .WithTags("Teams");
    app.MapPortiaGet<GetTeam, TeamView>("/api/v1/tenants/{tenantId}/teams/{teamId}")
        .RequireAuthorization()
        .WithTags("Teams");
    app.MapPortiaGet<ListTeams, Page<TeamView>>("/api/v1/tenants/{tenantId}/teams")
        .RequireAuthorization()
        .WithTags("Teams");
    app.MapPortiaPost<AssignTeamMember>("/api/v1/tenants/{tenantId}/teams/{teamId}/members/{memberId}")
        .RequireAuthorization()
        .WithTags("Teams");
    app.MapPortiaDelete<RemoveTeamMember>("/api/v1/tenants/{tenantId}/teams/{teamId}/members/{memberId}")
        .RequireAuthorization()
        .WithTags("Teams");
    app.MapPortiaGet<ListTeamMembers, Page<TeamMemberView>>("/api/v1/tenants/{tenantId}/teams/{teamId}/members")
        .RequireAuthorization()
        .WithTags("Teams");
    app.MapPortiaPost<AssignTeamRole>("/api/v1/tenants/{tenantId}/teams/{teamId}/roles/{roleId}")
        .RequireAuthorization()
        .WithTags("Teams");
    app.MapPortiaDelete<RemoveTeamRole>("/api/v1/tenants/{tenantId}/teams/{teamId}/roles/{roleId}")
        .RequireAuthorization()
        .WithTags("Teams");
    app.MapPortiaPost<DefineRole>("/api/v1/tenants/{tenantId}/roles/{roleId}")
        .RequireAuthorization()
        .WithTags("Roles");
    app.MapPortiaDelete<DeleteRole>("/api/v1/tenants/{tenantId}/roles/{roleId}")
        .RequireAuthorization()
        .WithTags("Roles");
    app.MapPortiaGet<GetRole, RoleView>("/api/v1/tenants/{tenantId}/roles/{roleId}")
        .RequireAuthorization()
        .WithTags("Roles");
    app.MapPortiaGet<ListRoles, Page<RoleView>>("/api/v1/tenants/{tenantId}/roles")
        .RequireAuthorization()
        .WithTags("Roles");
    app.MapPortiaPost<AssignRolePermission>("/api/v1/tenants/{tenantId}/roles/{roleId}/permissions/{permission}")
        .RequireAuthorization()
        .WithTags("Roles");
    app.MapPortiaDelete<RemoveRolePermission>("/api/v1/tenants/{tenantId}/roles/{roleId}/permissions/{permission}")
        .RequireAuthorization()
        .WithTags("Roles");
    app.MapPortiaGet<ListRolePermissions, Page<RolePermissionView>>(
            "/api/v1/tenants/{tenantId}/roles/{roleId}/permissions")
        .RequireAuthorization()
        .WithTags("Roles");
    app.MapPortiaGet<ListRoleTeams, Page<RoleTeamView>>("/api/v1/tenants/{tenantId}/roles/{roleId}/teams")
        .RequireAuthorization()
        .WithTags("Roles");
    app.MapMethods(
        "/api/{**path}",
        ["DELETE", "GET", "HEAD", "OPTIONS", "PATCH", "POST", "PUT"],
        () => Results.NotFound())
        .RequireAuthorization()
        .ExcludeFromDescription();

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

/// <summary>
/// Exposes the application entry point to integration tests.
/// </summary>
public partial class Program
{
}

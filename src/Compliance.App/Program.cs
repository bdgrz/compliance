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
        .AddMcpTool<ListProgramRevisions>(tool => tool.ReadOnly())
        .AddMcpTool<CreateBoundary>()
        .AddMcpTool<ReviseBoundaryDraft>(tool => tool.Idempotent())
        .AddMcpTool<DiscardBoundaryDraft>(tool => tool.Destructive())
        .AddMcpTool<GetBoundary>(tool => tool.ReadOnly())
        .AddMcpTool<ListProgramBoundaries>(tool => tool.ReadOnly())
        .AddMcpTool<GetBoundaryVersion>(tool => tool.ReadOnly())
        .AddMcpTool<ListBoundaryVersions>(tool => tool.ReadOnly())
        .AddMcpTool<GetEffectiveBoundaryVersion>(tool => tool.ReadOnly())
        .AddMcpTool<GetBoundaryDecision>(tool => tool.ReadOnly())
        .AddMcpTool<ListBoundaryDecisions>(tool => tool.ReadOnly())
        .AddMcpTool<PreviewBoundaryImpact>(tool => tool.ReadOnly())
        .AddMcpTool<ProposeBoundarySuccessor>()
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

    app.Use((context, next) =>
    {
        // Organization slugs may identify a client and must not be sent to
        // another origin through a browser's Referer header.
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        return next(context);
    });
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
    app.MapPortiaMcp("/mcp").RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser);

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
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
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
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaPost<SuspendTenant>("/api/v1/tenants/{tenantId}/suspensions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaDelete<ReactivateTenant>("/api/v1/tenants/{tenantId}/suspensions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaPost<InviteTenantMember>("/api/v1/tenants/{tenantId}/invitations")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaPost<AcceptTenantInvitation>("/api/v1/tenants/{tenantId}/invitations/acceptance")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaGet<GetTenant, TenantView>("/api/v1/tenants/{tenantId}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaGet<ListTenantMembers, Page<TenantMembershipView>>("/api/v1/tenants/{tenantId}/members")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaPost<ChangeTenantSlug>("/api/v1/tenants/{tenantId}/slug-changes")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaGet<ResolveMyTenantSlug, TenantSlugResolution>("/api/v1/tenant-slugs/{slug}/mine")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaPost<CreateProgram, ProgramRegistration>("/api/v1/tenants/{tenantId}/programs")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Programs");
    app.MapPortiaPut<ReviseProgram>("/api/v1/tenants/{tenantId}/programs/{programId}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Programs");
    app.MapPortiaGet<GetProgram, ProgramView>("/api/v1/tenants/{tenantId}/programs/{programId}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Programs");
    app.MapPortiaGet<ListPrograms, Page<ProgramView>>("/api/v1/tenants/{tenantId}/programs")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Programs");
    app.MapPortiaGet<ListProgramRevisions, Page<ProgramRevisionView>>(
            "/api/v1/tenants/{tenantId}/programs/{programId}/revisions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Programs");
    app.MapPortiaPost<CreateBoundary, BoundaryRegistration>(
            "/api/v1/tenants/{tenantId}/programs/{programId}/boundaries")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaGet<ListProgramBoundaries, Page<BoundaryView>>(
            "/api/v1/tenants/{tenantId}/programs/{programId}/boundaries")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaPut<ReviseBoundaryDraft>(
            "/api/v1/tenants/{tenantId}/boundaries/{boundaryId}/drafts/{draftVersionId}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaPost<DiscardBoundaryDraft>(
            "/api/v1/tenants/{tenantId}/boundaries/{boundaryId}/drafts/{draftVersionId}/discards")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaGet<GetBoundary, BoundaryView>(
            "/api/v1/tenants/{tenantId}/boundaries/{boundaryId}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaGet<GetBoundaryVersion, BoundaryVersionView>(
            "/api/v1/tenants/{tenantId}/boundaries/{boundaryId}/versions/{versionId}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaGet<ListBoundaryVersions, Page<BoundaryVersionView>>(
            "/api/v1/tenants/{tenantId}/boundaries/{boundaryId}/versions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaGet<GetEffectiveBoundaryVersion, BoundaryVersionView>(
            "/api/v1/tenants/{tenantId}/boundaries/{boundaryId}/effective-version")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaGet<GetBoundaryDecision, BoundaryDecisionView>(
            "/api/v1/tenants/{tenantId}/boundaries/{boundaryId}/decisions/{decisionId}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaGet<ListBoundaryDecisions, Page<BoundaryDecisionView>>(
            "/api/v1/tenants/{tenantId}/boundaries/{boundaryId}/decisions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaGet<PreviewBoundaryImpact, BoundaryImpactPreview>(
            "/api/v1/tenants/{tenantId}/boundaries/{boundaryId}/drafts/{draftVersionId}/impact-preview")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaPost<ReviewBoundary>(
            "/api/v1/tenants/{tenantId}/boundaries/{boundaryId}/drafts/{draftVersionId}/reviews")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaPost<ApproveBoundary>(
            "/api/v1/tenants/{tenantId}/boundaries/{boundaryId}/drafts/{draftVersionId}/approvals")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaPost<ProposeBoundarySuccessor, BoundaryRegistration>(
            "/api/v1/tenants/{tenantId}/boundaries/{boundaryId}/successors")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaPost<ReserveEmail>("/api/v1/users/{userId}/email-addresses/{emailAddress}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Email addresses");
    app.MapPortiaPost<IssueEmailChallenge>("/api/v1/users/{userId}/email-addresses/{emailAddress}/challenges")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Email addresses");
    app.MapPortiaPost<CompleteEmailChallenge>("/api/v1/users/{userId}/email-addresses/{emailAddress}/verifications")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Email addresses");
    app.MapPortiaGet<GetEmailAddress, EmailAddressView>("/api/v1/users/{userId}/email-addresses/{emailAddress}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Email addresses");
    app.MapPortiaGet<ListEmailAddresses, Page<EmailAddressView>>("/api/v1/users/{userId}/email-addresses")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Email addresses");
    app.MapPortiaGet<ListMyTenants, Page<TenantMembershipSummary>>("/api/v1/tenants/mine")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaPost<DefineTeam>("/api/v1/tenants/{tenantId}/teams/{teamId}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Teams");
    app.MapPortiaDelete<DeleteTeam>("/api/v1/tenants/{tenantId}/teams/{teamId}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Teams");
    app.MapPortiaGet<GetTeam, TeamView>("/api/v1/tenants/{tenantId}/teams/{teamId}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Teams");
    app.MapPortiaGet<ListTeams, Page<TeamView>>("/api/v1/tenants/{tenantId}/teams")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Teams");
    app.MapPortiaPost<AssignTeamMember>("/api/v1/tenants/{tenantId}/teams/{teamId}/members/{memberId}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Teams");
    app.MapPortiaDelete<RemoveTeamMember>("/api/v1/tenants/{tenantId}/teams/{teamId}/members/{memberId}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Teams");
    app.MapPortiaGet<ListTeamMembers, Page<TeamMemberView>>("/api/v1/tenants/{tenantId}/teams/{teamId}/members")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Teams");
    app.MapPortiaPost<AssignTeamRole>("/api/v1/tenants/{tenantId}/teams/{teamId}/roles/{roleId}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Teams");
    app.MapPortiaDelete<RemoveTeamRole>("/api/v1/tenants/{tenantId}/teams/{teamId}/roles/{roleId}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Teams");
    app.MapPortiaPost<DefineRole>("/api/v1/tenants/{tenantId}/roles/{roleId}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Roles");
    app.MapPortiaDelete<DeleteRole>("/api/v1/tenants/{tenantId}/roles/{roleId}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Roles");
    app.MapPortiaGet<GetRole, RoleView>("/api/v1/tenants/{tenantId}/roles/{roleId}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Roles");
    app.MapPortiaGet<ListRoles, Page<RoleView>>("/api/v1/tenants/{tenantId}/roles")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Roles");
    app.MapPortiaPost<AssignRolePermission>("/api/v1/tenants/{tenantId}/roles/{roleId}/permissions/{permission}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Roles");
    app.MapPortiaDelete<RemoveRolePermission>("/api/v1/tenants/{tenantId}/roles/{roleId}/permissions/{permission}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Roles");
    app.MapPortiaGet<ListRolePermissions, Page<RolePermissionView>>(
            "/api/v1/tenants/{tenantId}/roles/{roleId}/permissions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Roles");
    app.MapPortiaGet<ListRoleTeams, Page<RoleTeamView>>("/api/v1/tenants/{tenantId}/roles/{roleId}/teams")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Roles");
    app.MapMethods(
        "/api/{**path}",
        ["DELETE", "GET", "HEAD", "OPTIONS", "PATCH", "POST", "PUT"],
        () => Results.NotFound())
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
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

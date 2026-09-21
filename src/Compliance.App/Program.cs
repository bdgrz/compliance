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
        .AddMcpTool<InviteOrganizationMember>()
        .AddMcpTool<ListTenantInvitations>(tool => tool.ReadOnly())
        .AddMcpTool<GetMemberAccess>(tool => tool.ReadOnly())
        .AddMcpTool<GetTenant>(tool => tool.ReadOnly())
        .AddMcpTool<ListTenantMembers>(tool => tool.ReadOnly())
        .AddMcpTool<ChangeTenantSlug>()
        .AddMcpTool<ResolveMyTenantSlug>(tool => tool.ReadOnly())
        .AddMcpTool<CreateProgram>()
        .AddMcpTool<GetControlDraft>(tool => tool.ReadOnly())
        .AddMcpTool<ListControlDrafts>(tool => tool.ReadOnly())
        .AddMcpTool<GetControlDraftRevision>(tool => tool.ReadOnly())
        .AddMcpTool<ReviseProgram>(tool => tool.Idempotent())
        .AddMcpTool<GetProgram>(tool => tool.ReadOnly())
        .AddMcpTool<ListPrograms>(tool => tool.ReadOnly())
        .AddMcpTool<ListProgramRevisions>(tool => tool.ReadOnly())
        .AddMcpTool<GetProgramRevision>(tool => tool.ReadOnly())
        .AddMcpTool<GetProgramSetupWork>(tool => tool.ReadOnly())
        .AddMcpTool<DeclareApplication>()
        .AddMcpTool<ReviseApplication>(tool => tool.Idempotent())
        .AddMcpTool<DeclareSystemInstance>()
        .AddMcpTool<GetApplication>(tool => tool.ReadOnly())
        .AddMcpTool<ListApplications>(tool => tool.ReadOnly())
        .AddMcpTool<GetApplicationRevision>(tool => tool.ReadOnly())
        .AddMcpTool<ListApplicationRevisions>(tool => tool.ReadOnly())
        .AddMcpTool<GetSystemInstance>(tool => tool.ReadOnly())
        .AddMcpTool<ListSystemInstances>(tool => tool.ReadOnly())
        .AddMcpTool<ListApplicationBoundaryReferences>(tool => tool.ReadOnly())
        .AddMcpTool<ListSystemInstanceBoundaryReferences>(tool => tool.ReadOnly())
        // Portia 0.5.2 MCP binding rejects object arrays; register staging after the framework fix.
        .AddMcpTool<GetApplicationImport>(tool => tool.ReadOnly())
        .AddMcpTool<ListApplicationImportRows>(tool => tool.ReadOnly())
        .AddMcpTool<PreviewApplicationImport>(tool => tool.ReadOnly())
        .AddMcpTool<CreateClientService>()
        .AddMcpTool<ReviseClientService>(tool => tool.Idempotent())
        .AddMcpTool<RetireClientService>(tool => tool.Destructive())
        .AddMcpTool<GetClientService>(tool => tool.ReadOnly())
        .AddMcpTool<ListClientServices>(tool => tool.ReadOnly())
        .AddMcpTool<ListProgramClientServices>(tool => tool.ReadOnly())
        .AddMcpTool<ListClientServiceRevisions>(tool => tool.ReadOnly())
        .AddMcpTool<GetClientServiceRevision>(tool => tool.ReadOnly())
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
        .AddMcpTool<FreezeProgramScopeSnapshot>()
        .AddMcpTool<AmendProgramScopeSnapshot>()
        .AddMcpTool<GetSnapshot>(tool => tool.ReadOnly())
        .AddMcpTool<VerifyProgramScopeSnapshot>(tool => tool.ReadOnly())
        .AddMcpTool<ListProgramSnapshots>(tool => tool.ReadOnly())
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
        app.MapPortiaPost<LinkOidcProviderIdentity, AuthenticatedUserIdentity>(
                "/api/v1/my/oidc_identity_links")
            .RequireAuthorization(ComplianceAuthorizationPolicies.IdentityLink)
            .WithTags("Users");
    }
    app.MapPortiaPost<RegisterTenant, TenantRegistration>("/api/v1/tenants")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaPost<SuspendTenant>("/api/v1/tenants/{tenant_id}/suspensions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaDelete<ReactivateTenant>("/api/v1/tenants/{tenant_id}/suspensions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaPost<InviteTenantMember>("/api/v1/tenants/{tenant_id}/invitations")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaPost<InviteOrganizationMember>("/api/v1/tenants/{tenant_id}/member_invitations")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaGet<ListTenantInvitations, Page<TenantInvitationView>>(
            "/api/v1/tenants/{tenant_id}/member_invitations")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaGet<GetMemberAccess, MemberAccessView>(
            "/api/v1/tenants/{tenant_id}/members/{user_id}/access")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Access control");
    app.MapPortiaPost<AcceptTenantInvitation>("/api/v1/tenants/{tenant_id}/invitations/acceptance")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaGet<GetTenant, TenantView>("/api/v1/tenants/{tenant_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaGet<ListTenantMembers, Page<TenantMembershipView>>("/api/v1/tenants/{tenant_id}/members")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaPost<ChangeTenantSlug>("/api/v1/tenants/{tenant_id}/slug-changes")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaGet<ResolveMyTenantSlug, TenantSlugResolution>("/api/v1/tenant-slugs/{slug}/mine")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaPost<CreateProgram, ProgramRegistration>("/api/v1/tenants/{tenant_id}/programs")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Programs");
    app.MapPortiaPut<ReviseProgram>("/api/v1/tenants/{tenant_id}/programs/{program_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Programs");
    app.MapPortiaGet<GetProgram, ProgramView>("/api/v1/tenants/{tenant_id}/programs/{program_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Programs");
    app.MapPortiaGet<ListPrograms, Page<ProgramView>>("/api/v1/tenants/{tenant_id}/programs")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Programs");
    app.MapPortiaGet<ListProgramRevisions, Page<ProgramRevisionView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/revisions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Programs");
    app.MapPortiaGet<GetProgramRevision, ProgramRevisionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/revisions/{revision}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Programs");
    app.MapPortiaGet<GetProgramSetupWork, ProgramSetupWorkView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/setup-work")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Programs");
    app.MapPortiaPost<CreateControlDraft, ControlRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaPut<ReviseControlDraft>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaGet<GetControlDraft, ControlDraftView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaGet<ListControlDrafts, Page<ControlDraftView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaGet<GetControlDraftRevision, ControlDraftRevisionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/revisions/{revision}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaPost<DeclareApplication, ApplicationRegistration>(
            "/api/v1/tenants/{tenant_id}/applications")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Applications");
    app.MapPortiaPut<ReviseApplication>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Applications");
    app.MapPortiaGet<GetApplication, ApplicationView>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Applications");
    app.MapPortiaGet<ListApplications, Page<ApplicationView>>(
            "/api/v1/tenants/{tenant_id}/applications")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Applications");
    app.MapPortiaGet<GetApplicationRevision, ApplicationRevisionView>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/revisions/{revision}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Applications");
    app.MapPortiaGet<ListApplicationRevisions, Page<ApplicationRevisionView>>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/revisions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Applications");
    app.MapPortiaPost<DeclareSystemInstance, SystemInstanceRegistration>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/system_instances")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("System instances");
    app.MapPortiaGet<GetSystemInstance, SystemInstanceView>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/system_instances/{system_instance_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("System instances");
    app.MapPortiaGet<ListSystemInstances, Page<SystemInstanceView>>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/system_instances")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("System instances");
    app.MapPortiaGet<ListApplicationBoundaryReferences,
            Page<ApplicationBoundaryReferenceView>>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/boundary_references")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("List current draft and approved boundary references for an application")
        .WithTags("Applications");
    app.MapPortiaGet<ListSystemInstanceBoundaryReferences,
            Page<ApplicationBoundaryReferenceView>>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/system_instances/{system_instance_id}/boundary_references")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("List current draft and approved boundary references for a system instance")
        .WithTags("System instances");
    app.MapPortiaPost<StageApplicationImport, ApplicationImportRegistration>(
            "/api/v1/tenants/{tenant_id}/application_imports")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Application imports");
    app.MapPortiaGet<GetApplicationImport, ApplicationImportView>(
            "/api/v1/tenants/{tenant_id}/application_imports/{batch_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Application imports");
    app.MapPortiaGet<ListApplicationImportRows, Page<ApplicationImportRowView>>(
            "/api/v1/tenants/{tenant_id}/application_imports/{batch_id}/rows")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Application imports");
    app.MapPortiaGet<PreviewApplicationImport, Page<ApplicationImportPreviewRow>>(
            "/api/v1/tenants/{tenant_id}/application_imports/{batch_id}/preview")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Application imports");
    app.MapPortiaPost<CreateClientService, ClientServiceRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/client-services")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Client services");
    app.MapPortiaGet<ListProgramClientServices, Page<ClientServiceView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/client-services")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Client services");
    app.MapPortiaPut<ReviseClientService>(
            "/api/v1/tenants/{tenant_id}/client-services/{service_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Client services");
    app.MapPortiaPost<RetireClientService>(
            "/api/v1/tenants/{tenant_id}/client-services/{service_id}/retirements")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Client services");
    app.MapPortiaGet<GetClientService, ClientServiceView>(
            "/api/v1/tenants/{tenant_id}/client-services/{service_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Client services");
    app.MapPortiaGet<ListClientServices, Page<ClientServiceView>>(
            "/api/v1/tenants/{tenant_id}/client-services")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Client services");
    app.MapPortiaGet<ListClientServiceRevisions, Page<ClientServiceRevisionView>>(
            "/api/v1/tenants/{tenant_id}/client-services/{service_id}/revisions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Client services");
    app.MapPortiaGet<GetClientServiceRevision, ClientServiceRevisionView>(
            "/api/v1/tenants/{tenant_id}/client_services/{service_id}/revisions/{revision}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Client services");
    app.MapPortiaPost<CreateBoundary, BoundaryRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/boundaries")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaGet<ListProgramBoundaries, Page<BoundaryView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/boundaries")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaPut<ReviseBoundaryDraft>(
            "/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/drafts/{draft_version_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaPost<DiscardBoundaryDraft>(
            "/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/drafts/{draft_version_id}/discards")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaGet<GetBoundary, BoundaryView>(
            "/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaGet<GetBoundaryVersion, BoundaryVersionView>(
            "/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/versions/{version_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaGet<ListBoundaryVersions, Page<BoundaryVersionView>>(
            "/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/versions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaGet<GetEffectiveBoundaryVersion, BoundaryVersionView>(
            "/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/effective_version")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaGet<GetBoundaryDecision, BoundaryDecisionView>(
            "/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/decisions/{decision_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaGet<ListBoundaryDecisions, Page<BoundaryDecisionView>>(
            "/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/decisions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaPost<FreezeProgramScopeSnapshot, SnapshotRegistration>(
            "/api/v1/tenants/{tenant_id}/scope_snapshots")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Snapshots");
    app.MapPortiaPost<AmendProgramScopeSnapshot, SnapshotRegistration>(
            "/api/v1/tenants/{tenant_id}/scope_snapshots/{snapshot_id}/amendments")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Snapshots");
    app.MapPortiaGet<GetSnapshot, SnapshotView>(
            "/api/v1/tenants/{tenant_id}/scope_snapshots/{snapshot_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Snapshots");
    app.MapPortiaGet<VerifyProgramScopeSnapshot, ProgramScopeSnapshotVerification>(
            "/api/v1/tenants/{tenant_id}/scope_snapshots/{snapshot_id}/verification")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Snapshots");
    app.MapPortiaGet<ListProgramSnapshots, Page<SnapshotView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/scope_snapshots")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Snapshots");
    app.MapPortiaGet<PreviewBoundaryImpact, BoundaryImpactPreview>(
            "/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/drafts/{draft_version_id}/impact_preview")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaPost<ReviewBoundary>(
            "/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/drafts/{draft_version_id}/reviews")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaPost<ApproveBoundary>(
            "/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/drafts/{draft_version_id}/approvals")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaPost<ProposeBoundarySuccessor, BoundaryRegistration>(
            "/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/successors")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaPost<ReserveEmail>("/api/v1/users/{user_id}/email-addresses/{email_address}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Email addresses");
    app.MapPortiaPost<IssueEmailChallenge>("/api/v1/users/{user_id}/email-addresses/{email_address}/challenges")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Email addresses");
    app.MapPortiaPost<CompleteEmailChallenge>("/api/v1/users/{user_id}/email-addresses/{email_address}/verifications")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Email addresses");
    app.MapPortiaGet<GetEmailAddress, EmailAddressView>("/api/v1/users/{user_id}/email-addresses/{email_address}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Email addresses");
    app.MapPortiaGet<ListEmailAddresses, Page<EmailAddressView>>("/api/v1/users/{user_id}/email-addresses")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Email addresses");
    app.MapPortiaGet<ListMyTenants, Page<TenantMembershipSummary>>("/api/v1/tenants/mine")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaPost<DefineTeam>("/api/v1/tenants/{tenant_id}/teams/{team_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Teams");
    app.MapPortiaDelete<DeleteTeam>("/api/v1/tenants/{tenant_id}/teams/{team_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Teams");
    app.MapPortiaGet<GetTeam, TeamView>("/api/v1/tenants/{tenant_id}/teams/{team_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Teams");
    app.MapPortiaGet<ListTeams, Page<TeamView>>("/api/v1/tenants/{tenant_id}/teams")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Teams");
    app.MapPortiaPost<AssignTeamMember>("/api/v1/tenants/{tenant_id}/teams/{team_id}/members/{member_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Teams");
    app.MapPortiaDelete<RemoveTeamMember>("/api/v1/tenants/{tenant_id}/teams/{team_id}/members/{member_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Teams");
    app.MapPortiaGet<ListTeamMembers, Page<TeamMemberView>>("/api/v1/tenants/{tenant_id}/teams/{team_id}/members")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Teams");
    app.MapPortiaPost<AssignTeamRole>("/api/v1/tenants/{tenant_id}/teams/{team_id}/roles/{role_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Teams");
    app.MapPortiaDelete<RemoveTeamRole>("/api/v1/tenants/{tenant_id}/teams/{team_id}/roles/{role_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Teams");
    app.MapPortiaPost<DefineRole>("/api/v1/tenants/{tenant_id}/roles/{role_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Roles");
    app.MapPortiaDelete<DeleteRole>("/api/v1/tenants/{tenant_id}/roles/{role_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Roles");
    app.MapPortiaGet<GetRole, RoleView>("/api/v1/tenants/{tenant_id}/roles/{role_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Roles");
    app.MapPortiaGet<ListRoles, Page<RoleView>>("/api/v1/tenants/{tenant_id}/roles")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Roles");
    app.MapPortiaPost<AssignRolePermission>("/api/v1/tenants/{tenant_id}/roles/{role_id}/permissions/{permission}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Roles");
    app.MapPortiaDelete<RemoveRolePermission>("/api/v1/tenants/{tenant_id}/roles/{role_id}/permissions/{permission}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Roles");
    app.MapPortiaGet<ListRolePermissions, Page<RolePermissionView>>(
            "/api/v1/tenants/{tenant_id}/roles/{role_id}/permissions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Roles");
    app.MapPortiaGet<ListRoleTeams, Page<RoleTeamView>>("/api/v1/tenants/{tenant_id}/roles/{role_id}/teams")
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

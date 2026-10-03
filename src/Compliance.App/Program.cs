using System.Globalization;
using System.Text.Json;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.Responsibilities;
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
    // Configuration includes environment variables; reading it here also lets a test host set
    // its mode without mutating the process environment.
    hostMode = ComplianceHostModeParser.Parse(builder.Configuration["COMPLIANCE_HOST_MODE"]);
    builder.Services.AddProblemDetails(options =>
    {
        options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["trace_id"] = context.HttpContext.TraceIdentifier;
    });
    var authentication = builder.Services.AddComplianceAuthentication(
        builder.Configuration,
        builder.Environment);
    var developerAuthentication = authentication is null;
    var portia = builder.Services.AddCompliance(builder.Configuration, developerAuthentication,
        requireRealEmailDelivery: !builder.Environment.IsDevelopment());
    // AddPortia returns the same builder and lets this host contribute its generated JSON context.
    builder.Services.AddPortia()
        .AddHttp()
        .AddMcpTool<DefineTeam>(tool => tool.Idempotent())
        .AddMcpTool<DeleteTeam>(tool => tool.Destructive())
        .AddMcpTool<GetTeam>(tool => tool.ReadOnly())
        .AddMcpTool<ListTeams>(tool => tool.ReadOnly())
        .AddMcpTool<AssignTeamMember>(tool => tool.Idempotent())
        .AddMcpTool<RemoveTeamMember>(tool => tool.Destructive())
        .AddMcpTool<SuspendMember>(tool => tool.Destructive())
        .AddMcpTool<ReinstateMember>(tool => tool.Idempotent())
        .AddMcpTool<GetTenantMember>(tool => tool.ReadOnly())
        .AddMcpTool<ListMemberResponsibilities>(tool => tool.ReadOnly())
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
        .AddMcpTool<ListTeamRoles>(tool => tool.ReadOnly())
        .AddMcpTool<GrantAccess>(tool => tool.Idempotent())
        .AddMcpTool<RevokeAccessGrant>(tool => tool.Destructive())
        .AddMcpTool<ListAccessGrants>(tool => tool.ReadOnly())
        .AddMcpTool<ListMyTenants>(tool => tool.ReadOnly())
        .AddMcpTool<RegisterTenant>()
        .AddMcpTool<SuspendTenant>(tool => tool.Destructive())
        .AddMcpTool<ReactivateTenant>(tool => tool.Idempotent())
        .AddMcpTool<GrantPlatformOperator>()
        .AddMcpTool<RevokePlatformOperator>(tool => tool.Destructive())
        .AddMcpTool<ListPlatformOperators>(tool => tool.ReadOnly())
        .AddMcpTool<InviteTenantMember>()
        .AddMcpTool<InviteOrganizationMember>()
        .AddMcpTool<ListTenantInvitations>(tool => tool.ReadOnly())
        .AddMcpTool<GetMemberAccess>(tool => tool.ReadOnly())
        .AddMcpTool<GetTenant>(tool => tool.ReadOnly())
        .AddMcpTool<ListTenants>(tool => tool.ReadOnly())
        .AddMcpTool<ListTenantMembers>(tool => tool.ReadOnly())
        .AddMcpTool<ChangeTenantSlug>()
        .AddMcpTool<ResolveMyTenantSlug>(tool => tool.ReadOnly())
        .AddMcpTool<CreateProgram>()
        .AddMcpTool<ListCriteriaCatalogEditions>(tool => tool.ReadOnly())
        .AddMcpTool<GetCriteriaCatalogEdition>(tool => tool.ReadOnly())
        .AddMcpTool<ListCriteriaCatalogEntries>(tool => tool.ReadOnly())
        .AddMcpTool<GetCriteriaCatalogEntry>(tool => tool.ReadOnly())
        .AddMcpTool<ExportCriteriaCatalogEntries>(tool => tool.ReadOnly())
        .AddMcpTool<SetCriteriaTextOverlay>(tool => tool.Idempotent())
        .AddMcpTool<SelectProgramCriteriaEdition>(tool => tool.Idempotent())
        .AddMcpTool<CreateControlDraft>()
        .AddMcpTool<ReviseControlDraft>(tool => tool.Idempotent())
        .AddMcpTool<DiscardControlDraft>(tool => tool.Destructive())
        .AddMcpTool<GetControlDraft>(tool => tool.ReadOnly())
        .AddMcpTool<ListControlDrafts>(tool => tool.ReadOnly())
        .AddMcpTool<ListControlDraftRevisions>(tool => tool.ReadOnly())
        .AddMcpTool<GetControlDraftRevision>(tool => tool.ReadOnly())
        .AddMcpTool<GetControlVersion>(tool => tool.ReadOnly())
        .AddMcpTool<GetCurrentControlVersion>(tool => tool.ReadOnly())
        .AddMcpTool<GetEffectiveControlVersion>(tool => tool.ReadOnly())
        .AddMcpTool<ListControlVersions>(tool => tool.ReadOnly())
        .AddMcpTool<GetControlDecision>(tool => tool.ReadOnly())
        .AddMcpTool<ListControlDecisions>(tool => tool.ReadOnly())
        .AddMcpTool<GetControlCriterionMapping>(tool => tool.ReadOnly())
        .AddMcpTool<ListControlCriterionMappings>(tool => tool.ReadOnly())
        .AddMcpTool<ListCriteriaCoverage>(tool => tool.ReadOnly())
        .AddMcpTool<ProposeControlSuccessor>()
        .AddMcpTool<ProposeControlRetirement>()
        .AddMcpTool<PreviewControlImpact>(tool => tool.ReadOnly())
        .AddMcpTool<GetCriterionApplicability>(tool => tool.ReadOnly())
        .AddMcpTool<ListCriterionApplicability>(tool => tool.ReadOnly())
        .AddMcpTool<CreateCommitmentDraft>()
        .AddMcpTool<ReviseCommitmentDraft>()
        .AddMcpTool<GetCommitmentDraft>(tool => tool.ReadOnly())
        .AddMcpTool<ListCommitmentDrafts>(tool => tool.ReadOnly())
        .AddMcpTool<ListCommitmentDraftRevisions>(tool => tool.ReadOnly())
        .AddMcpTool<GetCommitmentDraftRevision>(tool => tool.ReadOnly())
        .AddMcpTool<PreviewCommitmentImpact>(tool => tool.ReadOnly())
        .AddMcpTool<GetCommitmentVersion>(tool => tool.ReadOnly())
        .AddMcpTool<GetEffectiveCommitmentVersion>(tool => tool.ReadOnly())
        .AddMcpTool<ListCommitmentVersions>(tool => tool.ReadOnly())
        .AddMcpTool<ListCommitmentDecisions>(tool => tool.ReadOnly())
        .AddMcpTool<CreatePolicyDraft>()
        .AddMcpTool<RevisePolicyDraft>()
        .AddMcpTool<ProposePolicySuccessor>()
        .AddMcpTool<DiscardPolicyDraft>(tool => tool.Destructive())
        .AddMcpTool<ProposePolicyRetirement>()
        .AddMcpTool<GetPolicy>(tool => tool.ReadOnly())
        .AddMcpTool<ListPolicies>(tool => tool.ReadOnly())
        .AddMcpTool<GetPolicyVersion>(tool => tool.ReadOnly())
        .AddMcpTool<GetEffectivePolicyVersion>(tool => tool.ReadOnly())
        .AddMcpTool<ListPolicyVersions>(tool => tool.ReadOnly())
        .AddMcpTool<ListPolicyDecisions>(tool => tool.ReadOnly())
        .AddMcpTool<PreviewPolicyImpact>(tool => tool.ReadOnly())
        .AddMcpTool<DefineTrainingRequirement>()
        .AddMcpTool<ReviseTrainingRequirement>()
        .AddMcpTool<GetTrainingRequirement>(tool => tool.ReadOnly())
        .AddMcpTool<ListTrainingRequirements>(tool => tool.ReadOnly())
        .AddMcpTool<LaunchPolicyCampaign>()
        .AddMcpTool<LaunchTrainingCampaign>()
        .AddMcpTool<ReconcileCampaignAudience>()
        .AddMcpTool<RecordTrainingCompletion>()
        .AddMcpTool<CloseCampaign>()
        .AddMcpTool<GetCampaign>(tool => tool.ReadOnly())
        .AddMcpTool<ListCampaigns>(tool => tool.ReadOnly())
        .AddMcpTool<ListCampaignParticipants>(tool => tool.ReadOnly())
        .AddMcpTool<ListCampaignAmendments>(tool => tool.ReadOnly())
        .AddMcpTool<CreateRiskDraft>()
        .AddMcpTool<ReviseRiskDraft>(tool => tool.Idempotent())
        .AddMcpTool<GetRiskDraft>(tool => tool.ReadOnly())
        .AddMcpTool<ListRiskDrafts>(tool => tool.ReadOnly())
        .AddMcpTool<ListRiskDraftRevisions>(tool => tool.ReadOnly())
        .AddMcpTool<GetRiskDraftRevision>(tool => tool.ReadOnly())
        .AddMcpTool<GetRiskMethod>(tool => tool.ReadOnly())
        .AddMcpTool<GetRiskMethodVersion>(tool => tool.ReadOnly())
        .AddMcpTool<GetRiskEvaluation>(tool => tool.ReadOnly())
        .AddMcpTool<ListRiskEvaluationHistory>(tool => tool.ReadOnly())
        .AddMcpTool<GetRiskGovernance>(tool => tool.ReadOnly())
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
        .AddMcpTool<PreviewApplicationChange>(tool => tool.ReadOnly())
        .AddMcpTool<RetireApplication>(tool => tool.Idempotent())
        .AddMcpTool<RetireSystemInstance>(tool => tool.Idempotent())
        .AddMcpTool<GetAccessReviewScope>(tool => tool.ReadOnly())
        .AddMcpTool<ListAccessReviewScopes>(tool => tool.ReadOnly())
        .AddMcpTool<StageApplicationImport>(tool => tool.Idempotent())
        .AddMcpTool<GetApplicationImport>(tool => tool.ReadOnly())
        .AddMcpTool<ListApplicationImportRows>(tool => tool.ReadOnly())
        .AddMcpTool<PreviewApplicationImport>(tool => tool.ReadOnly())
        .AddMcpTool<RecordPerson>()
        .AddMcpTool<RevisePerson>(tool => tool.Idempotent())
        .AddMcpTool<CorrelatePersonMembership>(tool => tool.Idempotent())
        .AddMcpTool<GetPerson>(tool => tool.ReadOnly())
        .AddMcpTool<ListPeople>(tool => tool.ReadOnly())
        .AddMcpTool<RecordWorkRelationship>()
        .AddMcpTool<ReviseWorkRelationship>(tool => tool.Idempotent())
        .AddMcpTool<GetWorkRelationship>(tool => tool.ReadOnly())
        .AddMcpTool<ListWorkRelationships>(tool => tool.ReadOnly())
        .AddMcpTool<ListWorkforceObservations>(tool => tool.ReadOnly())
        .AddMcpTool<ListWorkforceReconciliationObservations>(tool => tool.ReadOnly())
        .AddMcpTool<RecordWorkforceSourceObservation>(tool => tool.Idempotent())
        .AddMcpTool<GetWorkforceSourceObservation>(tool => tool.ReadOnly())
        .AddMcpTool<ListWorkforceSourceObservations>(tool => tool.ReadOnly())
        .AddMcpTool<PreviewWorkforceSourceObservation>(tool => tool.ReadOnly())
        .AddMcpTool<RecordServiceIdentity>()
        .AddMcpTool<ReviseServiceIdentity>(tool => tool.Idempotent())
        .AddMcpTool<GetServiceIdentity>(tool => tool.ReadOnly())
        .AddMcpTool<ListServiceIdentities>(tool => tool.ReadOnly())
        .AddMcpTool<RecordProvider>()
        .AddMcpTool<ReviseProvider>(tool => tool.Idempotent())
        .AddMcpTool<GetProvider>(tool => tool.ReadOnly().Idempotent())
        .AddMcpTool<ListProviders>(tool => tool.ReadOnly().Idempotent())
        .AddMcpTool<GetProviderRevision>(tool => tool.ReadOnly().Idempotent())
        .AddMcpTool<ListProviderRevisions>(tool => tool.ReadOnly().Idempotent())
        .AddMcpTool<RecordAssuranceReport>()
        .AddMcpTool<ReviseAssuranceReport>(tool => tool.Idempotent())
        .AddMcpTool<ListProviderAssuranceReports>(tool => tool.ReadOnly().Idempotent())
        .AddMcpTool<ListProviderReviews>(tool => tool.ReadOnly().Idempotent())
        .AddMcpTool<GetProviderAssuranceCoverage>(tool => tool.ReadOnly().Idempotent())
        .AddMcpTool<PreviewProviderChange>(tool => tool.ReadOnly().Idempotent())
        .AddMcpTool<RecordTechnologyComponent>()
        .AddMcpTool<ReviseTechnologyComponent>(tool => tool.Idempotent())
        .AddMcpTool<GetTechnologyComponent>(tool => tool.ReadOnly())
        .AddMcpTool<ListTechnologyComponentBoundaryReferences>(tool => tool.ReadOnly())
        .AddMcpTool<ListInformationAssetBoundaryReferences>(tool => tool.ReadOnly())
        .AddMcpTool<ListTechnologyComponents>(tool => tool.ReadOnly())
        .AddMcpTool<ListTechnologyComponentRevisions>(tool => tool.ReadOnly())
        .AddMcpTool<RecordInformationAsset>()
        .AddMcpTool<ReviseInformationAsset>(tool => tool.Idempotent())
        .AddMcpTool<GetInformationAsset>(tool => tool.ReadOnly())
        .AddMcpTool<ListInformationAssets>(tool => tool.ReadOnly())
        .AddMcpTool<ListInformationAssetRevisions>(tool => tool.ReadOnly())
        .AddMcpTool<PreviewInformationAssetChange>(tool => tool.ReadOnly())
        .AddMcpTool<RecordLocation>()
        .AddMcpTool<ReviseLocation>(tool => tool.Idempotent())
        .AddMcpTool<GetLocation>(tool => tool.ReadOnly())
        .AddMcpTool<ListLocations>(tool => tool.ReadOnly())
        .AddMcpTool<ListLocationRevisions>(tool => tool.ReadOnly())
        .AddMcpTool<RecordOperationalProcess>()
        .AddMcpTool<ReviseOperationalProcess>(tool => tool.Idempotent())
        .AddMcpTool<GetOperationalProcess>(tool => tool.ReadOnly())
        .AddMcpTool<ListOperationalProcesses>(tool => tool.ReadOnly())
        .AddMcpTool<ListOperationalProcessRevisions>(tool => tool.ReadOnly())
        .AddMcpTool<RecordDataFlow>()
        .AddMcpTool<ReviseDataFlow>(tool => tool.Idempotent())
        .AddMcpTool<GetDataFlow>(tool => tool.ReadOnly())
        .AddMcpTool<ListDataFlows>(tool => tool.ReadOnly())
        .AddMcpTool<ListDataFlowRevisions>(tool => tool.ReadOnly())
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
        .AddMcpTool<ListResponsibilities>(tool => tool.ReadOnly())
        .AddMcpTool<PreviewResponsibilityConflicts>(tool => tool.ReadOnly())
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
        .AddMcpTool<RegenerateProgramScopeSnapshotManifest>(tool => tool.ReadOnly())
        .AddMcpTool<ListProgramSnapshots>(tool => tool.ReadOnly())
        .AddMcpTool<FreezeWorkforceRosterSnapshot>()
        .AddMcpTool<AmendWorkforceRosterSnapshot>()
        .AddMcpTool<GetWorkforceRosterSnapshot>(tool => tool.ReadOnly())
        .AddMcpTool<GetWorkforceRosterSnapshotAsOf>(tool => tool.ReadOnly())
        .AddMcpTool<ListWorkforceRosterSnapshots>(tool => tool.ReadOnly())
        .AddMcpTool<RegenerateWorkforceRosterSnapshotManifest>(tool => tool.ReadOnly())
        .AddMcpTool<GetReadinessAssessment>(tool => tool.ReadOnly())
        .AddMcpTool<ListReadinessAssessments>(tool => tool.ReadOnly())
        .AddMcpTool<ListReadinessGaps>(tool => tool.ReadOnly())
        .AddMcpTool<ListTypeIEntryDecisions>(tool => tool.ReadOnly())
        .AddMcpTool<ListReadinessAnnotations>(tool => tool.ReadOnly())
        .AddMcpTool<ProposeControlOperatingPlan>()
        .AddMcpTool<PreviewControlOperatingPlan>(tool => tool.ReadOnly())
        .AddMcpTool<GetControlOperatingPlan>(tool => tool.ReadOnly())
        .AddMcpTool<ListControlOperatingBlockers>(tool => tool.ReadOnly())
        .AddMcpTool<ListMyControlWork>(tool => tool.ReadOnly())
        .AddMcpTool<GetControlOccurrence>(tool => tool.ReadOnly())
        .AddMcpTool<ListControlOccurrences>(tool => tool.ReadOnly())
        .AddMcpTool<StartControlEvaluation>()
        .AddMcpTool<RecordControlEvaluationStep>()
        .AddMcpTool<DisposeControlEvaluationDeviation>()
        .AddMcpTool<GetControlEvaluation>(tool => tool.ReadOnly())
        .AddMcpTool<ListControlEvaluations>(tool => tool.ReadOnly())
        .AddMcpTool<RaiseFinding>()
        .AddMcpTool<GetFinding>(tool => tool.ReadOnly())
        .AddMcpTool<ListFindings>(tool => tool.ReadOnly())
        .AddMcpTool<OpenEvidenceRequest>()
        .AddMcpTool<CancelEvidenceRequest>()
        .AddMcpTool<GetEvidenceRequest>(tool => tool.ReadOnly())
        .AddMcpTool<ListEvidenceRequests>(tool => tool.ReadOnly())
        .AddMcpTool<OpenAccessPopulation>()
        .AddMcpTool<RecordAccessPopulationFacts>(tool => tool.Idempotent())
        .AddMcpTool<PreviewAccessPopulation>(tool => tool.ReadOnly())
        .AddMcpTool<GetAccessPopulation>(tool => tool.ReadOnly())
        .AddMcpTool<ListAccessPopulations>(tool => tool.ReadOnly())
        .AddMcpTool<GetAccessReviewCoverage>(tool => tool.ReadOnly())
        .AddMcpTool<ClassifyAccessPrincipal>()
        .AddMcpTool<ListAccessPrincipals>(tool => tool.ReadOnly())
        .AddMcpTool<GetAccessVariance>(tool => tool.ReadOnly())
        .AddMcpTool<ProposeAccessExpectation>()
        .AddMcpTool<ListAccessExpectations>(tool => tool.ReadOnly())
        .AddMcpTool<LaunchAccessReviewCampaign>()
        .AddMcpTool<GetAccessReviewCampaign>(tool => tool.ReadOnly())
        .AddMcpTool<ListAccessReviewCampaigns>(tool => tool.ReadOnly())
        .AddMcpTool<PreviewBulkAccessDecision>(tool => tool.ReadOnly())
        .AddMcpTool<RecordAccessRemediationChange>()
        .AddMcpTool<VerifyAccessRemediation>()
        .AddMcpTool<ListWork>(tool => tool.ReadOnly())
        .AddMcpTool<GetWorkItem>(tool => tool.ReadOnly())
        .AddMcpTool<ClaimWorkItem>()
        .AddMcpTool<AssignWorkItem>()
        .AddMcpTool<DelegateWorkItem>()
        .AddMcpTool<EscalateWorkItem>()
        .AddMcpTool<ListWorkReminders>(tool => tool.ReadOnly())
        .AddMcpTool<GetWorkDigest>(tool => tool.ReadOnly())
        .AddMcpTool<GetWorkDigestPreference>(tool => tool.ReadOnly())
        .AddMcpTool<SetWorkDigestPreference>()
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
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddSingleton<AuthorizationDenialLog>();
    builder.Services.AddHostedService(services => services.GetRequiredService<AuthorizationDenialLog>());
    builder.Services.AddComplianceHealthChecks();
    builder.Services.AddTenantPathLogRedaction();

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
    app.MapPortiaMcp(AuthorizationDenialLog.McpPath).RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser);

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
                "/api/v1/my/oidc-identity-links")
            .RequireAuthorization(ComplianceAuthorizationPolicies.IdentityLink)
            .WithTags("Users");
        app.MapPortiaPost<StartIdentityRecovery>("/api/v1/identity-recovery/challenges")
            .AllowAnonymous()
            .WithTags("Users");
        app.MapPortiaPost<GetIdentityRecoveryOptions, IdentityRecoveryOptions>(
                "/api/v1/identity-recovery/options")
            .RequireAuthorization(ComplianceAuthorizationPolicies.OidcContinuation)
            .WithTags("Users");
        app.MapPortiaPost<CompleteIdentityRecovery>("/api/v1/identity-recovery/completions")
            .RequireAuthorization(ComplianceAuthorizationPolicies.OidcContinuation)
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
    app.MapPortiaPost<GrantPlatformOperator>("/api/v1/platform/operator-grants")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Platform operators");
    app.MapPortiaPost<RevokePlatformOperator>("/api/v1/platform/operator-revocations")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Platform operators");
    app.MapPortiaGet<ListPlatformOperators, PlatformOperatorRosterView>("/api/v1/platform/operators")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Platform operators");
    app.MapPortiaPost<InviteTenantMember>("/api/v1/tenants/{tenant_id}/invitations")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaPost<InviteOrganizationMember>("/api/v1/tenants/{tenant_id}/member-invitations")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaGet<ListTenantInvitations, Page<TenantInvitationView>>(
            "/api/v1/tenants/{tenant_id}/member-invitations")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaGet<GetMemberAccess, MemberAccessView>(
            "/api/v1/tenants/{tenant_id}/members/{user_id}/access")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Access control");
    app.MapPortiaPost<SuspendMember>(
            "/api/v1/tenants/{tenant_id}/members/{user_id}/suspensions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Access control");
    app.MapPortiaDelete<ReinstateMember>(
            "/api/v1/tenants/{tenant_id}/members/{user_id}/suspensions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Access control");
    app.MapPortiaGet<GetTenantMember, TenantMembershipView>(
            "/api/v1/tenants/{tenant_id}/members/{user_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Access control");
    app.MapPortiaGet<ListMemberResponsibilities, IReadOnlyList<ResponsibilityAssignmentView>>(
            "/api/v1/tenants/{tenant_id}/members/{user_id}/responsibilities")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Responsibilities");
    app.MapPortiaPost<AcceptTenantInvitation>("/api/v1/tenants/{tenant_id}/invitations/acceptance")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaGet<GetTenant, TenantView>("/api/v1/tenants/{tenant_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Tenants");
    app.MapPortiaGet<ListTenants, Page<TenantView>>("/api/v1/platform/tenants")
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
    app.MapPortiaGet<ListCriteriaCatalogEditions, IReadOnlyList<CriteriaCatalogEdition>>(
            "/api/v1/tenants/{tenant_id}/criteria-editions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Criteria");
    app.MapPortiaGet<GetCriteriaCatalogEdition, CriteriaCatalogEdition>(
            "/api/v1/tenants/{tenant_id}/criteria-editions/{edition_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Criteria");
    app.MapPortiaGet<ListCriteriaCatalogEntries, Page<Criterion>>(
            "/api/v1/tenants/{tenant_id}/criteria-editions/{edition_id}/entries")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Criteria");
    app.MapPortiaGet<ExportCriteriaCatalogEntries, Page<Criterion>>(
            "/api/v1/tenants/{tenant_id}/criteria-editions/{edition_id}/entries/export")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Criteria");
    app.MapPortiaGet<GetCriteriaCatalogEntry, Criterion>(
            "/api/v1/tenants/{tenant_id}/criteria-editions/{edition_id}/entries/{identifier}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Criteria");
    app.MapPortiaPut<SetCriteriaTextOverlay, CriteriaTextOverlayRegistration>(
            "/api/v1/tenants/{tenant_id}/criteria-editions/{edition_id}/entries/{identifier}/overlay")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Criteria");
    app.MapPortiaPut<SelectProgramCriteriaEdition>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/criteria-edition")
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
    app.MapPortiaPost<DiscardControlDraft>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/discards")
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
    app.MapPortiaGet<ListControlDraftRevisions, Page<ControlDraftRevisionView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/revisions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaGet<GetControlDraftRevision, ControlDraftRevisionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/revisions/{revision}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    // Review and approval are human decisions and stay HTTP-only; no MCP tools are registered.
    app.MapPortiaPost<ReviewControl>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/reviews")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaPost<ApproveControl>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/approvals")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaGet<GetCurrentControlVersion, ControlVersionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/current-version")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaGet<GetEffectiveControlVersion, ControlVersionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/effective-version")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaGet<ListControlVersions, Page<ControlVersionView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/versions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaGet<GetControlVersion, ControlVersionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/versions/{version_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaGet<ListControlDecisions, Page<ControlDecisionView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/decisions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaGet<GetControlDecision, ControlDecisionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/decisions/{decision_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    // Mapping writes, including review, are human decisions and stay HTTP-only.
    app.MapPortiaPost<ProposeControlCriterionMapping, ControlCriterionMappingRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/control-mappings")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control mappings");
    app.MapPortiaPost<ReviewControlCriterionMapping>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/control-mappings/{mapping_id}/reviews")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control mappings");
    app.MapPortiaPost<RetireControlCriterionMapping>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/control-mappings/{mapping_id}/retirements")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control mappings");
    app.MapPortiaGet<GetControlCriterionMapping, ControlCriterionMappingView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/control-mappings/{mapping_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control mappings");
    app.MapPortiaGet<ListControlCriterionMappings, Page<ControlCriterionMappingView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/control-mappings")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control mappings");
    app.MapPortiaGet<ListCriteriaCoverage, Page<CriterionCoverageView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/criteria-coverage")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control mappings");
    app.MapPortiaPost<ProposeControlSuccessor, ControlSuccessorRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/successors")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaPost<ProposeControlRetirement, ControlRetirementRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/retirement-proposals")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaGet<PreviewControlImpact, ControlImpactPreview>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/impact-preview")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    // Retirement approval is a human decision and stays HTTP-only.
    app.MapPortiaPost<RetireControl>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/retirements")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    // Withdrawal and person-owner designation are human decisions and stay HTTP-only.
    app.MapPortiaPost<WithdrawControlProposal>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/proposal-withdrawals")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    app.MapPortiaPut<DesignateControlOwnerPerson>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/owner-person")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Controls");
    // Criterion applicability decisions are human decisions; writes stay HTTP-only.
    app.MapPortiaPost<ProposeCriterionNotApplicable, CriterionApplicabilityRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/criterion-applicability")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control mappings");
    app.MapPortiaPost<ReviewCriterionApplicability>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/criterion-applicability/{decision_id}/reviews")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control mappings");
    app.MapPortiaPost<WithdrawCriterionNotApplicable>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/criterion-applicability/{decision_id}/withdrawals")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control mappings");
    app.MapPortiaGet<GetCriterionApplicability, CriterionApplicabilityView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/criterion-applicability/{decision_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control mappings");
    app.MapPortiaGet<ListCriterionApplicability, Page<CriterionApplicabilityView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/criterion-applicability")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control mappings");
    app.MapPortiaPost<CreateCommitmentDraft, CommitmentDraftRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Commitments");
    app.MapPortiaPut<ReviseCommitmentDraft>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Commitments");
    app.MapPortiaGet<GetCommitmentDraft, CommitmentDraftView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Commitments");
    app.MapPortiaGet<ListCommitmentDrafts, Page<CommitmentDraftView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Commitments");
    app.MapPortiaGet<ListCommitmentDraftRevisions, Page<CommitmentDraftRevisionView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/revisions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Commitments");
    app.MapPortiaGet<GetCommitmentDraftRevision, CommitmentDraftRevisionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/revisions/{revision}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Commitments");
    app.MapPortiaGet<PreviewCommitmentImpact, CommitmentImpactPreview>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/impact-preview")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Commitments");
    app.MapPortiaPost<ReviewCommitmentDraft>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/reviews")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Commitments");
    // Approval is a separate human decision after review and stays HTTP-only.
    app.MapPortiaPost<ApproveCommitmentDraft>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/approvals")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Commitments");
    app.MapPortiaGet<ListCommitmentDecisions, Page<CommitmentDecisionView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/decisions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Commitments");
    app.MapPortiaGet<ListCommitmentVersions, Page<CommitmentVersionView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/versions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Commitments");
    app.MapPortiaGet<GetCommitmentVersion, CommitmentVersionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/versions/{version}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Commitments");
    app.MapPortiaGet<GetEffectiveCommitmentVersion, CommitmentVersionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/effective-version")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Commitments");
    app.MapPortiaPost<CreatePolicyDraft, PolicyRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/policies")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaGet<ListPolicies, Page<PolicySummaryView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/policies")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaGet<GetPolicy, PolicyView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaPut<RevisePolicyDraft, PolicyRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/draft")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaPost<DiscardPolicyDraft>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/draft/discards")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaPost<ProposePolicySuccessor, PolicyRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/successors")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaGet<PreviewPolicyImpact, PolicyImpactPreview>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/impact-preview")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    // Reviews, approvals, periodic reviews, and retirement approvals are personal decisions and stay HTTP-only.
    app.MapPortiaPost<ReviewPolicyDraft, PolicyDecisionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/reviews")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaPost<ApprovePolicy, PolicyVersionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/approvals")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaPost<ConfirmPolicyReview, PolicyDecisionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/periodic-reviews")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaPost<ProposePolicyRetirement, PolicyRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/retirement-proposals")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaPost<ApprovePolicyRetirement, PolicyDecisionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/retirements")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaGet<ListPolicyVersions, Page<PolicyVersionView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/versions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaGet<GetPolicyVersion, PolicyVersionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/versions/{version}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaGet<GetEffectivePolicyVersion, PolicyVersionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/effective-version")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaGet<ListPolicyDecisions, Page<PolicyDecisionView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/decisions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policies");
    app.MapPortiaPost<DefineTrainingRequirement, TrainingRequirementRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/training-requirements")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policy distribution");
    app.MapPortiaGet<ListTrainingRequirements, Page<TrainingRequirementView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/training-requirements")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policy distribution");
    app.MapPortiaGet<GetTrainingRequirement, TrainingRequirementView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/training-requirements/{requirement_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policy distribution");
    app.MapPortiaPut<ReviseTrainingRequirement, TrainingRequirementRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/training-requirements/{requirement_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policy distribution");
    app.MapPortiaPost<LaunchPolicyCampaign, CampaignRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/policy-campaigns")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policy distribution");
    app.MapPortiaPost<LaunchTrainingCampaign, CampaignRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/training-campaigns")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policy distribution");
    app.MapPortiaGet<ListCampaigns, Page<CampaignSummaryView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policy distribution");
    app.MapPortiaGet<GetCampaign, CampaignView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policy distribution");
    app.MapPortiaGet<ListCampaignParticipants, Page<CampaignParticipantView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/participants")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policy distribution");
    app.MapPortiaGet<ListCampaignAmendments, Page<CampaignAmendmentView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/amendments")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policy distribution");
    app.MapPortiaPost<ReconcileCampaignAudience, CampaignReconciliationView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/reconciliations")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policy distribution");
    app.MapPortiaPost<RecordTrainingCompletion, CampaignCompletionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/completions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policy distribution");
    app.MapPortiaPost<CloseCampaign, CampaignView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/closure")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policy distribution");
    // Personal acknowledgements and exception approvals stay HTTP-only.
    app.MapPortiaPost<AcknowledgePolicy, CampaignAcknowledgementView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/acknowledgements")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policy distribution");
    app.MapPortiaPost<ApproveCampaignWaiver, CampaignWaiverView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/waivers")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Policy distribution");
    app.MapPortiaPost<CreateRiskDraft, RiskRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPut<ReviseRiskDraft>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/draft")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaGet<GetRiskDraft, RiskDraftView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/draft")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaGet<ListRiskDrafts, Page<RiskDraftView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaGet<ListRiskDraftRevisions, Page<RiskDraftRevisionView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/draft/revisions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaGet<GetRiskDraftRevision, RiskDraftRevisionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/draft/revisions/{revision}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPost<PublishRiskMethodVersion, RiskMethodVersionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risk-method/versions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaGet<GetRiskMethod, RiskMethodVersionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risk-method")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaGet<GetRiskMethodVersion, RiskMethodVersionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risk-method/versions/{version}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPost<RecordRiskAssessment, RiskAssessmentView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/assessments")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPut<ChooseRiskTreatment>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/treatment")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPost<AcceptRisk, RiskAcceptanceView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/acceptances")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaGet<GetRiskEvaluation, RiskEvaluationView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/evaluation")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaGet<ListRiskEvaluationHistory, Page<RiskEvaluationHistoryEntryView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/evaluation/history")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    // Risk owner and control treatment writes are human decisions and stay HTTP-only.
    app.MapPortiaPut<AssignRiskOwner>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/owner")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPost<ProposeRiskControlTreatment, RiskControlTreatmentRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/control-treatments")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPost<ReviewRiskControlTreatment>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/control-treatments/{treatment_id}/reviews")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPost<RetireRiskControlTreatment>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/control-treatments/{treatment_id}/retirements")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPost<AddRiskTreatmentAction, RiskTreatmentActionRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/treatment-actions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPut<ReviseRiskTreatmentAction>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/treatment-actions/{action_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPost<CancelRiskTreatmentAction>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/treatment-actions/{action_id}/cancellations")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPost<SubmitRiskTreatmentActionCompletion,
            RiskTreatmentActionCompletionRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/treatment-actions/{action_id}/completions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPost<ReviewRiskTreatmentActionCompletion>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/treatment-actions/{action_id}/completion-reviews")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaGet<GetRiskGovernance, RiskGovernanceView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/governance")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Risks");
    app.MapPortiaPost<RunReadinessAssessment, ReadinessAssessmentRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Readiness");
    app.MapPortiaGet<ListReadinessAssessments, Page<ReadinessAssessmentSummaryView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Readiness");
    app.MapPortiaGet<GetReadinessAssessment, ReadinessAssessmentView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments/{assessment_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Readiness");
    app.MapPortiaGet<ListReadinessGaps, Page<ReadinessGapView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments/{assessment_id}/gaps")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Readiness");
    app.MapPortiaPost<DecideReadiness, ReadinessDecisionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments/{assessment_id}/decision")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Readiness");
    app.MapPortiaPut<PlanReadinessGap, ReadinessGapPlanView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/gaps/{gap_id}/plan")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Readiness");
    // The Type I entry decision is a personal sign-off and advisor feedback is attributed to
    // its author; both stay HTTP-only.
    app.MapPortiaPost<DecideTypeIEntry, TypeIEntryDecisionView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments/{assessment_id}/type-i-entry-decision")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Readiness");
    app.MapPortiaGet<ListTypeIEntryDecisions, Page<TypeIEntryDecisionView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/type-i-entry-decisions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Readiness");
    app.MapPortiaPost<AnnotateReadinessGap, ReadinessAnnotationView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments/{assessment_id}/gaps/{gap_id}/annotations")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Readiness");
    app.MapPortiaGet<ListReadinessAnnotations, Page<ReadinessAnnotationView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments/{assessment_id}/annotations")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Readiness");
    app.MapPortiaPost<ProposeControlOperatingPlan, ControlOperatingPlanView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/operating-plan/proposals")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control operations");
    app.MapPortiaPost<PreviewControlOperatingPlan, ControlOperatingPlanPreview>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/operating-plan/preview")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control operations");
    app.MapPortiaPost<ApproveControlOperatingPlan, ControlOperatingPlanView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/operating-plan/proposals/{plan_version_id}/approvals")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control operations");
    app.MapPortiaGet<GetControlOperatingPlan, ControlOperatingPlanSetView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/operating-plan")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control operations");
    app.MapPortiaGet<ListControlOperatingBlockers, Page<ControlOperatingBlockerView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/operating-blockers")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control operations");
    app.MapPortiaGet<ListMyControlWork, MyControlWorkView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/my-work")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control operations");
    app.MapPortiaPost<OpenControlOccurrence, ControlOccurrenceView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/occurrences")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control operations");
    app.MapPortiaGet<ListControlOccurrences, Page<ControlOccurrenceView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/occurrences")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control operations");
    app.MapPortiaGet<GetControlOccurrence, ControlOccurrenceView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/occurrences/{occurrence_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control operations");
    app.MapPortiaPost<AttestControlOccurrence, ControlOccurrenceView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/occurrences/{occurrence_id}/attestations")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control operations");
    app.MapPortiaPost<CorrectControlAttestation, ControlOccurrenceView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/occurrences/{occurrence_id}/corrections")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control operations");
    app.MapPortiaPost<ReviewControlOccurrence, ControlOccurrenceView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/occurrences/{occurrence_id}/reviews")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control operations");
    app.MapPortiaPost<StartControlEvaluation, ControlEvaluationView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control evaluations");
    app.MapPortiaGet<ListControlEvaluations, Page<ControlEvaluationView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control evaluations");
    app.MapPortiaGet<GetControlEvaluation, ControlEvaluationView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control evaluations");
    app.MapPortiaPost<RecordControlEvaluationStep, ControlEvaluationView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id}/steps/{step_id}/results")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control evaluations");
    app.MapPortiaPost<DisposeControlEvaluationDeviation, ControlEvaluationView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id}/deviations/{deviation_id}/dispositions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control evaluations");
    app.MapPortiaPost<SubmitControlEvaluation, ControlEvaluationView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id}/submissions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control evaluations");
    app.MapPortiaPost<ReviewControlEvaluation, ControlEvaluationView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id}/reviews")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Control evaluations");
    app.MapPortiaPost<OpenEvidenceRequest, EvidenceRequestView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/evidence-requests")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Evidence requests");
    app.MapPortiaGet<ListEvidenceRequests, Page<EvidenceRequestView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/evidence-requests")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Evidence requests");
    app.MapPortiaGet<GetEvidenceRequest, EvidenceRequestView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/evidence-requests/{evidence_request_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Evidence requests");
    app.MapPortiaPost<FulfilEvidenceRequest, EvidenceRequestView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/evidence-requests/{evidence_request_id}/fulfilments")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Evidence requests");
    app.MapPortiaPost<CancelEvidenceRequest, EvidenceRequestView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/evidence-requests/{evidence_request_id}/cancellations")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Evidence requests");
    app.MapPortiaPost<RaiseFinding, FindingRegistration>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/findings")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Findings");
    app.MapPortiaGet<ListFindings, Page<FindingView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/findings")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Findings");
    app.MapPortiaGet<GetFinding, FindingView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Findings");
    app.MapPortiaPost<ReviseFinding, FindingView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/revisions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Findings");
    app.MapPortiaPost<AddCorrectiveAction, FindingView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/corrective-actions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Findings");
    app.MapPortiaPost<CompleteCorrectiveAction, FindingView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/corrective-actions/{action_id}/completions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Findings");
    app.MapPortiaPost<LinkFindingAcceptance, FindingView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/acceptances")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Findings");
    app.MapPortiaPost<CloseFinding, FindingView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/closures")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Findings");
    app.MapPortiaPost<ReopenFinding, FindingView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/reopenings")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Findings");
    app.MapPortiaPost<OpenAccessPopulation, AccessPopulationRegistration>(
            "/api/v1/tenants/{tenant_id}/access-populations")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPut<RecordAccessPopulationFacts, AccessPopulationRegistration>(
            "/api/v1/tenants/{tenant_id}/access-populations/{population_id}/facts")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaGet<PreviewAccessPopulation, AccessPopulationPreview>(
            "/api/v1/tenants/{tenant_id}/access-populations/{population_id}/preview")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPost<AcceptAccessPopulation, AccessPopulationAcceptance>(
            "/api/v1/tenants/{tenant_id}/access-populations/{population_id}/acceptance")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaGet<GetAccessPopulation, AccessPopulationView>(
            "/api/v1/tenants/{tenant_id}/access-populations/{population_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaGet<ListAccessPopulations, Page<AccessPopulationSummaryView>>(
            "/api/v1/tenants/{tenant_id}/system-instances/{system_instance_id}/access-populations")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPost<ExemptMissingAccessPopulation, AccessPopulationExceptionView>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}/access-population-exceptions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaGet<GetAccessReviewCoverage, AccessReviewCoverageView>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/access-review-coverage")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPost<ClassifyAccessPrincipal, AccessPrincipalClassificationView>(
            "/api/v1/tenants/{tenant_id}/access-populations/{population_id}/classifications")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaGet<ListAccessPrincipals, Page<AccessPrincipalView>>(
            "/api/v1/tenants/{tenant_id}/access-populations/{population_id}/principals")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaGet<GetAccessVariance, AccessVarianceView>(
            "/api/v1/tenants/{tenant_id}/access-populations/{population_id}/variance")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPost<ProposeAccessExpectation, AccessExpectationView>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}/access-expectations")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPost<ApproveAccessExpectation, AccessExpectationView>(
            "/api/v1/tenants/{tenant_id}/system-instances/{system_instance_id}/access-expectations/{expectation_id}/approval")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPost<ExemptAccessExpectation, AccessExpectationExceptionView>(
            "/api/v1/tenants/{tenant_id}/system-instances/{system_instance_id}/access-expectations/{expectation_id}/exceptions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaGet<ListAccessExpectations, AccessExpectationsView>(
            "/api/v1/tenants/{tenant_id}/system-instances/{system_instance_id}/access-expectations")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPost<LaunchAccessReviewCampaign, AccessReviewCampaignRegistration>(
            "/api/v1/tenants/{tenant_id}/access-review-campaigns")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaGet<ListAccessReviewCampaigns, Page<AccessReviewCampaignSummaryView>>(
            "/api/v1/tenants/{tenant_id}/access-review-campaigns")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaGet<GetAccessReviewCampaign, AccessReviewCampaignView>(
            "/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPost<RecordAccessDecision, AccessDecisionView>(
            "/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/items/{item_id}/decisions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPost<PreviewBulkAccessDecision, BulkAccessDecisionPreview>(
            "/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/bulk-decision-previews")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPost<RecordBulkAccessDecision, BulkAccessDecisionResult>(
            "/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/bulk-decisions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPost<RecordAccessRemediationChange, AccessRemediationChangeView>(
            "/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/items/{item_id}/remediation-changes")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPost<VerifyAccessRemediation, AccessRemediationVerificationView>(
            "/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/items/{item_id}/remediation-verifications")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPost<ExemptAccessRemediation, AccessRemediationExceptionView>(
            "/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/items/{item_id}/remediation-exceptions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaPost<CompleteAccessReviewCampaign, AccessReviewCampaignCompletionView>(
            "/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/completion")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("AccessReviews");
    app.MapPortiaGet<ListWork, WorkQueueView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/work")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Work");
    app.MapPortiaGet<ListWorkReminders, IReadOnlyList<WorkReminderView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/work/reminders")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Work");
    app.MapPortiaGet<GetWorkDigest, WorkDigestView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/work/digest")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Work");
    app.MapPortiaGet<GetWorkItem, WorkItemDetailView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/work/{work_item_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Work");
    app.MapPortiaPost<ClaimWorkItem, WorkItemDetailView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/work/{work_item_id}/claims")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Work");
    app.MapPortiaPost<AssignWorkItem, WorkItemDetailView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/work/{work_item_id}/assignments")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Work");
    app.MapPortiaPost<DelegateWorkItem, WorkItemDetailView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/work/{work_item_id}/delegations")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Work");
    app.MapPortiaPost<EscalateWorkItem, WorkItemDetailView>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/work/{work_item_id}/escalations")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Work");
    app.MapPortiaGet<GetWorkDigestPreference, WorkDigestPreferenceView>(
            "/api/v1/tenants/{tenant_id}/work-digest-preference")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Work");
    app.MapPortiaPut<SetWorkDigestPreference, WorkDigestPreferenceView>(
            "/api/v1/tenants/{tenant_id}/work-digest-preference")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Work");
    app.MapPortiaPost<RecordPerson, PersonRegistration>(
            "/api/v1/tenants/{tenant_id}/people")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPut<RevisePerson>(
            "/api/v1/tenants/{tenant_id}/people/{person_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPut<CorrelatePersonMembership>(
            "/api/v1/tenants/{tenant_id}/people/{person_id}/membership-correlation")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaGet<GetPerson, PersonView>(
            "/api/v1/tenants/{tenant_id}/people/{person_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaGet<ListPeople, Page<PersonView>>(
            "/api/v1/tenants/{tenant_id}/people")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPost<RecordWorkRelationship, WorkRelationshipRegistration>(
            "/api/v1/tenants/{tenant_id}/work-relationships")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPut<ReviseWorkRelationship>(
            "/api/v1/tenants/{tenant_id}/work-relationships/{relationship_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaGet<GetWorkRelationship, WorkRelationshipView>(
            "/api/v1/tenants/{tenant_id}/work-relationships/{relationship_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaGet<ListWorkRelationships, Page<WorkRelationshipView>>(
            "/api/v1/tenants/{tenant_id}/work-relationships")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaGet<ListWorkforceObservations, Page<WorkforceObservationView>>(
            "/api/v1/tenants/{tenant_id}/workforce-observations")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPut<ResolveWorkforceObservation>(
            "/api/v1/tenants/{tenant_id}/workforce-observations/{observation_id}/resolution")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPost<RecordWorkforceSourceObservation, WorkforceSourceRegistration>(
            "/api/v1/tenants/{tenant_id}/workforce-source-observations")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaGet<GetWorkforceSourceObservation, WorkforceSourceView>(
            "/api/v1/tenants/{tenant_id}/workforce-source-observations/{observation_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaGet<ListWorkforceSourceObservations, Page<WorkforceSourceView>>(
            "/api/v1/tenants/{tenant_id}/workforce-source-observations")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaGet<PreviewWorkforceSourceObservation, WorkforceSourcePreview>(
            "/api/v1/tenants/{tenant_id}/workforce-source-observations/{observation_id}/preview")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPut<ReconcileWorkforceSourceObservation>(
            "/api/v1/tenants/{tenant_id}/workforce-source-observations/{observation_id}/decision")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaGet<ListWorkforceReconciliationObservations,
            Page<WorkforceReconciliationObservationView>>(
            "/api/v1/tenants/{tenant_id}/workforce-reconciliation-observations")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPost<RecordServiceIdentity, ServiceIdentityRegistration>(
            "/api/v1/tenants/{tenant_id}/service-identities")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPut<ReviseServiceIdentity>(
            "/api/v1/tenants/{tenant_id}/service-identities/{service_identity_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaGet<GetServiceIdentity, ServiceIdentityView>(
            "/api/v1/tenants/{tenant_id}/service-identities/{service_identity_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaGet<ListServiceIdentities, Page<ServiceIdentityView>>(
            "/api/v1/tenants/{tenant_id}/service-identities")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Workforce");
    app.MapPortiaPost<RecordProvider, ProviderRegistration>("/api/v1/tenants/{tenant_id}/providers")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    app.MapPortiaPut<ReviseProvider, ProviderRegistration>("/api/v1/tenants/{tenant_id}/providers/{provider_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    app.MapPortiaGet<GetProvider, ProviderView>("/api/v1/tenants/{tenant_id}/providers/{provider_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    app.MapPortiaGet<ListProviders, Page<ProviderView>>("/api/v1/tenants/{tenant_id}/providers")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    app.MapPortiaGet<GetProviderRevision, ProviderView>("/api/v1/tenants/{tenant_id}/providers/{provider_id}/revisions/{revision}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    app.MapPortiaGet<ListProviderRevisions, Page<ProviderView>>("/api/v1/tenants/{tenant_id}/providers/{provider_id}/revisions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    app.MapPortiaPost<RecordAssuranceReport, AssuranceReportRegistration>(
            "/api/v1/tenants/{tenant_id}/providers/{provider_id}/assurance-reports")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    app.MapPortiaPut<ReviseAssuranceReport, AssuranceReportRegistration>(
            "/api/v1/tenants/{tenant_id}/providers/{provider_id}/assurance-reports/{report_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    app.MapPortiaGet<ListProviderAssuranceReports, Page<AssuranceReportView>>(
            "/api/v1/tenants/{tenant_id}/providers/{provider_id}/assurance-reports")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    // Personal sign-off: HTTP-only, deliberately not an MCP tool.
    app.MapPortiaPost<RecordProviderReview, ProviderReviewRegistration>(
            "/api/v1/tenants/{tenant_id}/providers/{provider_id}/reviews")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    app.MapPortiaGet<ListProviderReviews, Page<ProviderReviewView>>(
            "/api/v1/tenants/{tenant_id}/providers/{provider_id}/reviews")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    app.MapPortiaGet<GetProviderAssuranceCoverage, ProviderAssuranceCoverageView>(
            "/api/v1/tenants/{tenant_id}/providers/{provider_id}/assurance-coverage")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Providers");
    app.MapPortiaPost<PreviewProviderChange, ProviderChangeImpactPreview>(
            "/api/v1/tenants/{tenant_id}/providers/{provider_id}/change-impact-previews")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("Preview provider change impact across systems, data, controls, evidence, and scope")
        .WithTags("Providers");
    app.MapPortiaPost<RecordTechnologyComponent, TechnologyComponentRegistration>(
            "/api/v1/tenants/{tenant_id}/technology-components")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaPut<ReviseTechnologyComponent>(
            "/api/v1/tenants/{tenant_id}/technology-components/{component_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<ListTechnologyComponentBoundaryReferences,
            Page<ApplicationBoundaryReferenceView>>(
            "/api/v1/tenants/{tenant_id}/technology-components/{component_id}/boundary-references")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("List current draft and approved boundary references for a technology component")
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<ListInformationAssetBoundaryReferences,
            Page<ApplicationBoundaryReferenceView>>(
            "/api/v1/tenants/{tenant_id}/information-assets/{information_asset_id}/boundary-references")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("List current draft and approved boundary references for an information asset")
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<GetTechnologyComponent, TechnologyComponentView>(
            "/api/v1/tenants/{tenant_id}/technology-components/{component_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<ListTechnologyComponents, Page<TechnologyComponentView>>(
            "/api/v1/tenants/{tenant_id}/technology-components")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<ListTechnologyComponentRevisions, Page<TechnologyComponentView>>(
            "/api/v1/tenants/{tenant_id}/technology-components/{component_id}/revisions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaPost<RecordInformationAsset, InformationAssetRegistration>(
            "/api/v1/tenants/{tenant_id}/information-assets")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaPut<ReviseInformationAsset>(
            "/api/v1/tenants/{tenant_id}/information-assets/{information_asset_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<GetInformationAsset, InformationAssetView>(
            "/api/v1/tenants/{tenant_id}/information-assets/{information_asset_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<ListInformationAssets, Page<InformationAssetView>>(
            "/api/v1/tenants/{tenant_id}/information-assets")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<ListInformationAssetRevisions, Page<InformationAssetView>>(
            "/api/v1/tenants/{tenant_id}/information-assets/{information_asset_id}/revisions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaPost<PreviewInformationAssetChange, InformationAssetChangePreview>(
            "/api/v1/tenants/{tenant_id}/information-assets/{information_asset_id}/change-previews")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("Preview the data-flow impact of reclassifying or retiring an information asset")
        .WithTags("TechnologyInventory");
    app.MapPortiaPost<RecordLocation, LocationRegistration>(
            "/api/v1/tenants/{tenant_id}/locations")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaPut<ReviseLocation>(
            "/api/v1/tenants/{tenant_id}/locations/{location_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<GetLocation, LocationView>(
            "/api/v1/tenants/{tenant_id}/locations/{location_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<ListLocations, Page<LocationView>>(
            "/api/v1/tenants/{tenant_id}/locations")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<ListLocationRevisions, Page<LocationView>>(
            "/api/v1/tenants/{tenant_id}/locations/{location_id}/revisions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaPost<RecordOperationalProcess, OperationalProcessRegistration>(
            "/api/v1/tenants/{tenant_id}/operational-processes")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaPut<ReviseOperationalProcess>(
            "/api/v1/tenants/{tenant_id}/operational-processes/{operational_process_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<GetOperationalProcess, OperationalProcessView>(
            "/api/v1/tenants/{tenant_id}/operational-processes/{operational_process_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<ListOperationalProcesses, Page<OperationalProcessView>>(
            "/api/v1/tenants/{tenant_id}/operational-processes")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<ListOperationalProcessRevisions, Page<OperationalProcessView>>(
            "/api/v1/tenants/{tenant_id}/operational-processes/{operational_process_id}/revisions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaPost<RecordDataFlow, DataFlowRegistration>(
            "/api/v1/tenants/{tenant_id}/data-flows")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaPut<ReviseDataFlow>(
            "/api/v1/tenants/{tenant_id}/data-flows/{data_flow_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<GetDataFlow, DataFlowView>(
            "/api/v1/tenants/{tenant_id}/data-flows/{data_flow_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<ListDataFlows, Page<DataFlowView>>(
            "/api/v1/tenants/{tenant_id}/data-flows")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
    app.MapPortiaGet<ListDataFlowRevisions, Page<DataFlowView>>(
            "/api/v1/tenants/{tenant_id}/data-flows/{data_flow_id}/revisions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("TechnologyInventory");
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
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("System instances");
    app.MapPortiaGet<GetSystemInstance, SystemInstanceView>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("System instances");
    app.MapPortiaGet<ListSystemInstances, Page<SystemInstanceView>>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("System instances");
    app.MapPortiaGet<ListApplicationBoundaryReferences,
            Page<ApplicationBoundaryReferenceView>>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/boundary-references")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("List current draft and approved boundary references for an application")
        .WithTags("Applications");
    app.MapPortiaGet<ListSystemInstanceBoundaryReferences,
            Page<ApplicationBoundaryReferenceView>>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}/boundary-references")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("List current draft and approved boundary references for a system instance")
        .WithTags("System instances");
    app.MapPortiaPost<PreviewApplicationChange, ApplicationChangePreview>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/change-previews")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("Preview the known impact of a proposed application change")
        .WithTags("Applications");
    app.MapPortiaPost<RetireApplication>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/retirements")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("Retire an application from an effective date, optionally merged into a successor")
        .WithTags("Applications");
    app.MapPortiaPost<RetireSystemInstance>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}/retirements")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("Retire a system instance from an effective date")
        .WithTags("System instances");
    app.MapPortiaPost<DecideAccessReviewScope, AccessReviewScopeDecisionView>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}/access-review-scope-decisions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("Approve an effective-dated access-review scope decision")
        .WithTags("System instances");
    app.MapPortiaGet<GetAccessReviewScope, AccessReviewScopeView>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}/access-review-scope")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("Read the effective access-review scope decision and its history")
        .WithTags("System instances");
    app.MapPortiaGet<ListAccessReviewScopes, Page<AccessReviewScopeStatusView>>(
            "/api/v1/tenants/{tenant_id}/applications/{application_id}/access-review-scopes")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithSummary("List the projected access-review scope status of an application's system instances")
        .WithTags("Applications");
    app.MapPortiaPost<StageApplicationImport, ApplicationImportRegistration>(
            "/api/v1/tenants/{tenant_id}/application-imports")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Application imports");
    app.MapPortiaPost<CancelApplicationImport>(
            "/api/v1/tenants/{tenant_id}/application-imports/{batch_id}/cancellations")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Application imports");
    app.MapPortiaGet<GetApplicationImport, ApplicationImportView>(
            "/api/v1/tenants/{tenant_id}/application-imports/{batch_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Application imports");
    app.MapPortiaGet<ListApplicationImportRows, Page<ApplicationImportRowView>>(
            "/api/v1/tenants/{tenant_id}/application-imports/{batch_id}/rows")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Application imports");
    app.MapPortiaGet<PreviewApplicationImport, Page<ApplicationImportPreviewRow>>(
            "/api/v1/tenants/{tenant_id}/application-imports/{batch_id}/preview")
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
            "/api/v1/tenants/{tenant_id}/client-services/{service_id}/revisions/{revision}")
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
    app.MapPortiaPost<AssignResponsibility>(
            "/api/v1/tenants/{tenant_id}/responsibilities")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Responsibilities");
    app.MapPortiaPost<RevokeResponsibility>(
            "/api/v1/tenants/{tenant_id}/responsibilities/{assignment_id}/revocation")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Responsibilities");
    app.MapPortiaGet<ListResponsibilities, ResponsibilitySetView>(
            "/api/v1/tenants/{tenant_id}/responsibilities")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Responsibilities");
    app.MapPortiaGet<PreviewResponsibilityConflicts, ResponsibilityConflictPreview>(
            "/api/v1/tenants/{tenant_id}/responsibilities/conflict-preview")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithDescription("The type query value must be control_owner, evidence_contributor, assigned_reviewer, access_reviewer, corrective_action_owner, or policy_approver.")
        .WithTags("Responsibilities");
    app.MapPortiaGet<GetBoundaryVersion, BoundaryVersionView>(
            "/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/versions/{version_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaGet<ListBoundaryVersions, Page<BoundaryVersionView>>(
            "/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/versions")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Boundaries");
    app.MapPortiaGet<GetEffectiveBoundaryVersion, BoundaryVersionView>(
            "/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/effective-version")
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
    app.MapPortiaGet<GetSeparationOfDutiesWaiver, SeparationOfDutiesWaiverView>(
            "/api/v1/tenants/{tenant_id}/separation-of-duties-waivers/{waiver_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Access control");
    app.MapPortiaPost<FreezeProgramScopeSnapshot, SnapshotRegistration>(
            "/api/v1/tenants/{tenant_id}/scope-snapshots")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Snapshots");
    app.MapPortiaPost<AmendProgramScopeSnapshot, SnapshotRegistration>(
            "/api/v1/tenants/{tenant_id}/scope-snapshots/{snapshot_id}/amendments")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Snapshots");
    app.MapPortiaGet<GetSnapshot, SnapshotView>(
            "/api/v1/tenants/{tenant_id}/scope-snapshots/{snapshot_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Snapshots");
    app.MapPortiaGet<VerifyProgramScopeSnapshot, ProgramScopeSnapshotVerification>(
            "/api/v1/tenants/{tenant_id}/scope-snapshots/{snapshot_id}/verification")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Snapshots");
    app.MapPortiaGet<RegenerateProgramScopeSnapshotManifest,
            ProgramScopeSnapshotManifestRegeneration>(
            "/api/v1/tenants/{tenant_id}/scope-snapshots/{snapshot_id}/manifest-regeneration")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Snapshots");
    app.MapPortiaGet<ListProgramSnapshots, Page<SnapshotView>>(
            "/api/v1/tenants/{tenant_id}/programs/{program_id}/scope-snapshots")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Snapshots");
    app.MapPortiaPost<FreezeWorkforceRosterSnapshot, SnapshotRegistration>(
            "/api/v1/tenants/{tenant_id}/workforce-roster-snapshots")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Snapshots");
    app.MapPortiaPost<AmendWorkforceRosterSnapshot, SnapshotRegistration>(
            "/api/v1/tenants/{tenant_id}/workforce-roster-snapshots/{snapshot_id}/amendments")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Snapshots");
    app.MapPortiaGet<GetWorkforceRosterSnapshot, WorkforceRosterSnapshotView>(
            "/api/v1/tenants/{tenant_id}/workforce-roster-snapshots/{snapshot_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Snapshots");
    app.MapPortiaGet<ListWorkforceRosterSnapshots, Page<PopulationSnapshotSummary>>(
            "/api/v1/tenants/{tenant_id}/workforce-roster-snapshots")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Snapshots");
    app.MapPortiaGet<RegenerateWorkforceRosterSnapshotManifest, PopulationSnapshotManifestRegeneration>(
            "/api/v1/tenants/{tenant_id}/workforce-roster-snapshots/{snapshot_id}/manifest-regeneration")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Snapshots");
    app.MapPortiaGet<GetWorkforceRosterSnapshotAsOf, WorkforceRosterSnapshotView>(
            "/api/v1/tenants/{tenant_id}/workforce-roster-snapshot-as-of")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Snapshots");
    app.MapPortiaGet<PreviewBoundaryImpact, BoundaryImpactPreview>(
            "/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/drafts/{draft_version_id}/impact-preview")
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
    app.MapPortiaPost<RecordSeparationOfDutiesWaiver, SeparationOfDutiesWaiverView>(
            "/api/v1/tenants/{tenant_id}/separation-of-duties-waivers")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Access control");
    app.MapPortiaPost<ApproveSeparationOfDutiesWaiver, SeparationOfDutiesWaiverView>(
            "/api/v1/tenants/{tenant_id}/separation-of-duties-waivers/{waiver_id}/approvals")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Access control");
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
    app.MapPortiaGet<GetEmailChallengeStatus, EmailChallengeStatusView>(
            "/api/v1/users/{user_id}/email-addresses/{email_address}/challenges/status")
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
    app.MapPortiaGet<ListTeamRoles, Page<TeamRoleView>>("/api/v1/tenants/{tenant_id}/teams/{team_id}/roles")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Teams");
    app.MapPortiaPost<GrantAccess>("/api/v1/tenants/{tenant_id}/access-grants/{grant_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Access grants");
    app.MapPortiaDelete<RevokeAccessGrant>("/api/v1/tenants/{tenant_id}/access-grants/{grant_id}")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Access grants");
    app.MapPortiaGet<ListAccessGrants, AccessGrantSetView>("/api/v1/tenants/{tenant_id}/access-grants")
        .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
        .WithTags("Access grants");
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
        .AddCompliance(builder.Configuration, developerAuthentication,
            requireRealEmailDelivery: !builder.Environment.IsDevelopment())
        .AddWorkers();
    builder.Services.AddComplianceHealthChecks();
    builder.Services.AddTenantPathLogRedaction();

    await builder.Build().RunAsync();
}

/// <summary>
/// Exposes the application entry point to integration tests.
/// </summary>
public partial class Program
{
}

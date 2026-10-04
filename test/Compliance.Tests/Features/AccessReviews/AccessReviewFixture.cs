using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Snapshots;
using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessReviews;

/// <summary>
///     Runs access-review requests through Portia's real lifecycle with the production authorizer,
///     an in-memory event store, the population snapshot primitive, and in-memory Fitz directories.
/// </summary>
sealed class AccessReviewFixture : IAsyncDisposable
{
    public static readonly DateTimeOffset Observed = DateTimeOffset.UtcNow.AddDays(-2);

    AccessReviewFixture(ServiceProvider provider, MemberPermissions permissions,
        FakeAccessReviewSources sources, FakeApplicationDirectory applications,
        bool applicationRestricted)
    {
        Provider = provider;
        Permissions = permissions;
        Sources = sources;
        Applications = applications;
        ApplicationRestricted = applicationRestricted;
    }

    public ServiceProvider Provider { get; }
    public MemberPermissions Permissions { get; }
    public FakeAccessReviewSources Sources { get; }
    public FakeApplicationDirectory Applications { get; }
    public bool ApplicationRestricted { get; }
    public Uuid TenantId { get; } = Uuid.CreateVersion4();
    public Uuid ApplicationId { get; } = Uuid.CreateVersion4();
    public Uuid InstanceId { get; } = Uuid.CreateVersion4();
    public Uuid ManagerUserId { get; } = Uuid.CreateVersion4();
    public Uuid ApproverUserId { get; } = Uuid.CreateVersion4();
    public Uuid ReviewerUserId { get; } = Uuid.CreateVersion4();
    public Uuid OutsiderUserId { get; } = Uuid.CreateVersion4();
    public Uuid ReviewerMemberId => RbacIds.Member(TenantId, ReviewerUserId);
    public Uuid ManagerMemberId => RbacIds.Member(TenantId, ManagerUserId);
    public Uuid AdaPersonId { get; } = Uuid.CreateVersion4();
    public Uuid ReviewerPersonId { get; } = Uuid.CreateVersion4();
    public Uuid BotIdentityId { get; } = Uuid.CreateVersion4();

    public static async Task<AccessReviewFixture> CreateAsync(bool applicationRestricted = false)
    {
        var permissions = new MemberPermissions();
        var sources = new FakeAccessReviewSources();
        var applications = new FakeApplicationDirectory();
        var provider = ProgramManagementServices.Build(permissions,
            portia => portia.AddRequestAuthorizer<AccessReviewAuthorizer>()
                .AddRequestHandler<OpenAccessPopulationHandler>()
                .AddRequestHandler<RecordAccessPopulationFactsHandler>()
                .AddRequestHandler<PreviewAccessPopulationHandler>()
                .AddRequestHandler<AcceptAccessPopulationHandler>()
                .AddRequestHandler<GetAccessPopulationHandler>()
                .AddRequestHandler<ListAccessPopulationsHandler>()
                .AddRequestHandler<ExemptMissingAccessPopulationHandler>()
                .AddRequestHandler<ClassifyAccessPrincipalHandler>()
                .AddRequestHandler<ListAccessPrincipalsHandler>()
                .AddRequestHandler<ProposeAccessExpectationHandler>()
                .AddRequestHandler<ApproveAccessExpectationHandler>()
                .AddRequestHandler<ExemptAccessExpectationHandler>()
                .AddRequestHandler<ListAccessExpectationsHandler>()
                .AddRequestHandler<GetAccessVarianceHandler>()
                .AddRequestHandler<LaunchAccessReviewCampaignHandler>()
                .AddRequestHandler<GetAccessReviewCampaignHandler>()
                .AddRequestHandler<RecordAccessDecisionHandler>()
                .AddRequestHandler<PreviewBulkAccessDecisionHandler>()
                .AddRequestHandler<RecordBulkAccessDecisionHandler>()
                .AddRequestHandler<RecordAccessRemediationChangeHandler>()
                .AddRequestHandler<VerifyAccessRemediationHandler>()
                .AddRequestHandler<ExemptAccessRemediationHandler>()
                .AddRequestHandler<CompleteAccessReviewCampaignHandler>(),
            services =>
            {
                var store = new InMemoryEventStore();
                services.AddSingleton<IEventStore>(store);
                services.AddSingleton<IDomainEventReader>(store);
                services.AddScoped<PopulationSnapshotFreezer>();
                services.AddScoped(provider => RestrictedApplicationVisibilityFixture.Create(
                    provider.GetRequiredService<IAggregateReader>(),
                    provider.GetRequiredService<IPermissionAuthorizer>(),
                    provider.GetRequiredService<IApplicationDirectoryReader>()));
                services.AddSingleton<IAccessReviewSources>(sources);
                services.AddSingleton<IApplicationDirectoryReader>(applications);
                services.AddSingleton<IAccessPopulationDirectoryReader>(
                    new FitzAccessPopulationDirectory(new InMemoryKvClient()));
            });
        var fixture = new AccessReviewFixture(provider, permissions, sources, applications,
            applicationRestricted);
        permissions.Allow(fixture.ManagerUserId);
        permissions.Allow(fixture.ApproverUserId);
        sources.AccessOwner = fixture.ReviewerMemberId;
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Seeder");
        var now = DateTimeOffset.UtcNow.AddDays(-10);
        applications.Instance = new SystemInstanceView(fixture.TenantId, fixture.ApplicationId,
            fixture.InstanceId, "Production", "aws_account", null, "manual", null, [],
            fixture.ManagerMemberId, "Manager", now)
        {
            Revision = 1,
        };
        await ProgramManagementServices.SeedAsync<DeclaredApplication, ApplicationRegistration>(
            provider, new DeclaredApplication(fixture.TenantId, fixture.ApplicationId),
            application => application.Declare("Payroll", "Run payroll", null,
                fixture.ManagerMemberId, "Manager", now, isRestricted: applicationRestricted));
        await ProgramManagementServices.SeedAsync(provider,
            new DeclaredSystemInstance(fixture.TenantId, fixture.InstanceId), instance =>
                instance.Declare(fixture.ApplicationId, "Production", "aws_account", null, null,
                    Uuid.CreateVersion4(), "Seeder", now));
        foreach (var (personId, name, email) in new[]
                 {
                     (fixture.AdaPersonId, "Ada", "ada@example.com"),
                     (fixture.ReviewerPersonId, "Rae", "rae@example.com"),
                 })
            await ProgramManagementServices.SeedAsync(provider,
                new Person(fixture.TenantId, personId), person => person.Record(name, email, actor, now));
        await ProgramManagementServices.SeedAsync(provider,
            new Person(fixture.TenantId, fixture.ReviewerPersonId), person =>
                person.CorrelateMembership(1, fixture.ReviewerUserId, actor, now) is null
                    ? Result.Success
                    : Result.Failure(new RequestError(RequestErrorKind.Conflict, "correlate")));
        await ProgramManagementServices.SeedAsync(provider,
            new ServiceIdentity(fixture.TenantId, fixture.BotIdentityId), identity =>
                identity.Record(new ServiceIdentityTerms("Deploy bot", "bot", "Deploys",
                    null, "active", "person", fixture.AdaPersonId,
                    DateOnly.FromDateTime(DateTime.UtcNow.AddDays(90))),
                    DateOnly.FromDateTime(DateTime.UtcNow), actor, now));
        sources.People =
        [
            Person(fixture, fixture.AdaPersonId, "Ada", "ada@example.com"),
            Person(fixture, fixture.ReviewerPersonId, "Rae", "rae@example.com"),
        ];
        return fixture;
    }

    static PersonView Person(AccessReviewFixture fixture, Uuid personId, string name, string email) =>
        new(fixture.TenantId, personId, 1, name, email, "manual",
            ActorReference.ForMember(Uuid.CreateVersion4(), "Seeder"), DateTimeOffset.UtcNow);

    /// <summary>
    ///     A standard population: Ada (user) is in Engineering, which is in Admins (admin
    ///     privilege), and can assume the Deployer role (deploy permission); Rae (user) holds the
    ///     admin privilege directly; the deploy bot (service principal) holds deploy directly; and
    ///     an IAM user principal holds read access.
    /// </summary>
    public static AccessPopulationFacts StandardFacts(bool raeHasAdmin = true) => new(
        [
            new("ada", "user_account", "Ada", "active", "ada@example.com"),
            new("rae", "user_account", "Rae", "active", "rae@example.com"),
            new("bot", "service_principal", "Deploy bot", "active"),
            new("legacy", "user_account", "Legacy IAM user", "active"),
            new("engineering", "group", "Engineering", "active"),
            new("admins", "group", "Admins", "active"),
            new("deployer", "role", "Deployer", "active"),
        ],
        [
            new("admin", "admin_privilege", "AdministratorAccess"),
            new("deploy", "permission", "Deploy"),
            new("read", "permission", "Read"),
        ],
        [
            new("engineering", "ada"),
            new("admins", "engineering"),
            new("deployer", "ada"),
        ],
        [
            .. raeHasAdmin ? new AccessAssignmentFact[] { new("rae", "admin") } : [],
            new("admins", "admin"),
            new("deployer", "deploy"),
            new("bot", "deploy"),
            new("legacy", "read"),
        ]);

    public RequestScenario As(Uuid userId) => RequestScenario.For(Provider)
        .GivenActor(ProgramManagementServices.Actor(userId));

    public async Task<TOut> SendAsync<TOut>(Uuid userId, IRequest<TOut> request)
    {
        var result = await As(userId).When(request).ExpectSuccess();
        return result.Value;
    }

    public Task<RequestError> FailAsync<TOut>(Uuid userId, IRequest<TOut> request,
        RequestErrorKind kind) => FailCoreAsync(userId, request, kind);

    async Task<RequestError> FailCoreAsync<TOut>(Uuid userId, IRequest<TOut> request,
        RequestErrorKind kind)
    {
        var result = await As(userId).When(request).ExpectFailure(kind);
        return Assert.IsType<RequestError>(result.Error);
    }

    /// <summary>Opens, records, and accepts a population; returns its ID and acceptance.</summary>
    public async Task<(Uuid PopulationId, AccessPopulationAcceptance Acceptance)> AcceptAsync(
        AccessPopulationFacts facts, DateTimeOffset? observedAt = null)
    {
        if (ApplicationRestricted)
            Permissions.AllowRestrictedRead(ManagerUserId);
        var opened = await SendAsync(ManagerUserId, new OpenAccessPopulation(TenantId,
            ApplicationId, InstanceId, 1, observedAt ?? Observed, "AWS console export, attested"));
        var recorded = await SendAsync(ManagerUserId, new RecordAccessPopulationFacts(TenantId,
            opened.PopulationId, 1, facts.Principals, facts.Entitlements, facts.GroupMembers,
            facts.Assignments));
        var accepted = await SendAsync(ManagerUserId, new AcceptAccessPopulation(TenantId,
            opened.PopulationId, recorded.Revision, "I attest this is the observed population."));
        if (ApplicationRestricted)
            Permissions.DenyRestrictedRead(ManagerUserId);
        return (opened.PopulationId, accepted);
    }

    public void AllowManagerRestrictedRead() => Permissions.AllowRestrictedRead(ManagerUserId);

    public void DenyManagerRestrictedRead() => Permissions.DenyRestrictedRead(ManagerUserId);

    public Task<AccessPrincipalClassificationView> ClassifyAsync(Uuid populationId, string subject,
        string classification, Uuid? personId = null, Uuid? serviceIdentityId = null, long count = 0) =>
        SendAsync(ManagerUserId, new ClassifyAccessPrincipal(TenantId, populationId, subject, count,
            classification, "Reviewed against the roster.", personId, serviceIdentityId));

    /// <summary>Classifies every subject of the standard population.</summary>
    public async Task ClassifyStandardAsync(Uuid populationId)
    {
        await ClassifyAsync(populationId, "ada", AccessReviewVocabulary.Human, AdaPersonId);
        await ClassifyAsync(populationId, "rae", AccessReviewVocabulary.Human, ReviewerPersonId);
        await ClassifyAsync(populationId, "bot", AccessReviewVocabulary.Nhi, serviceIdentityId: BotIdentityId);
        await SendAsync(ManagerUserId, new ClassifyAccessPrincipal(TenantId, populationId, "legacy",
            0, AccessReviewVocabulary.Shared, "Break-glass account.",
            AccountableOwnerPersonId: AdaPersonId, SharedJustification: "Emergency access."));
    }

    public async Task<AccessExpectationView> ApprovedExpectationAsync(long revision, string ruleKind,
        AccessExpectationParameters parameters)
    {
        var proposed = await SendAsync(ManagerUserId, new ProposeAccessExpectation(TenantId,
            ApplicationId, InstanceId, revision, ruleKind, parameters, "Initial expectation.",
            Observed.AddDays(-30)));
        return await SendAsync(ApproverUserId, new ApproveAccessExpectation(TenantId, InstanceId,
            proposed.ExpectationId, revision + 1));
    }

    public Task<AccessReviewCampaignRegistration> LaunchAsync(Uuid populationId,
        Uuid? reviewerMemberId = null, string? delegation = null) =>
        SendAsync(ManagerUserId, new LaunchAccessReviewCampaign(TenantId, "Q3 AWS review",
            "Keep only access each person still needs.", DateTimeOffset.UtcNow.AddDays(14),
            [new AccessReviewAssignment(populationId, reviewerMemberId ?? ReviewerMemberId, delegation)]));

    public Task<AccessReviewCampaignView> CampaignAsync(Uuid campaignId, Uuid? userId = null) =>
        SendAsync(userId ?? ManagerUserId, new GetAccessReviewCampaign(TenantId, campaignId));

    public ValueTask DisposeAsync() => Provider.DisposeAsync();

    public sealed class FakeApplicationDirectory : IApplicationDirectoryReader
    {
        public SystemInstanceView? Instance { get; set; }

        public ValueTask<SystemInstanceView?> GetInstanceAsync(Uuid tenantId, Uuid instanceId,
            CancellationToken ct = default) => ValueTask.FromResult(Instance is { } instance &&
                instance.TenantId == tenantId && instance.SystemInstanceId == instanceId
                ? instance
                : null);

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => throw new NotSupportedException();

        public ValueTask<ApplicationView?> GetAsync(Uuid tenantId, Uuid applicationId,
            CancellationToken ct = default) => throw new NotSupportedException();

        public ValueTask<Page<ApplicationView>> ListAsync(Uuid tenantId, int limit,
            string? cursor, CancellationToken ct = default) => throw new NotSupportedException();

        public ValueTask<ApplicationRevisionView?> GetRevisionAsync(Uuid tenantId,
            Uuid applicationId, long revision, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public ValueTask<Page<ApplicationRevisionView>?> ListRevisionsAsync(Uuid tenantId,
            Uuid applicationId, int limit, string? cursor, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public ValueTask<Page<SystemInstanceView>> ListInstancesAsync(Uuid tenantId,
            Uuid applicationId, int limit, string? cursor, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}

using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Features.ControlMappings;
using Bdgrz.Compliance.Features.Criteria;
using Bdgrz.Compliance.Features.Evaluations;
using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Readiness;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class AttestProgramManagementWriteWallTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid User = Uuid.CreateVersion4();
    static readonly Uuid Program = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("program")]
    [InlineData("service")]
    [InlineData("boundary")]
    [InlineData("commitment")]
    [InlineData("mapping")]
    [InlineData("overlay")]
    [InlineData("evaluation_plan")]
    [InlineData("finding")]
    [InlineData("readiness")]
    [InlineData("training")]
    public async Task ShouldDenyClientManagementRecordGivenActualAttestHistoryAndOrdinaryProgramGrant(string operation)
    {
        // Arrange
        await using var provider = Compose();
        await ProgramManagementServices.SeedAsync(provider, new ComplianceProgram(Tenant, Program), source =>
        {
            Assert.Null(source.Create("Security", new ProgramPlan(null, null, null, null, null, null),
                RbacIds.Member(Tenant, User), "Client administrator", Now));
            return Result.Success;
        });
        await AttestAssignmentHistoryFixture.SeedAsync(provider, Tenant, User, revoked: true);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var context = new RequestDispatchContext(ProgramManagementServices.Actor(User),
            new HttpInvocation("POST", "/synthetic/program-management", "/synthetic/program-management", "synthetic"));

        // Act
        var error = operation switch
        {
            "program" => (await bus.DispatchAsync(new CreateProgram(Tenant, "New program",
                new ProgramPlan(null, null, null, null, null, null)), context, CancellationToken.None)).Error,
            "service" => (await bus.DispatchAsync(new CreateClientService(Tenant, Program, "Service",
                "Client service purpose", "Client owner"), context, CancellationToken.None)).Error,
            "boundary" => (await bus.DispatchAsync(new CreateBoundary(Tenant, Program,
                new BoundaryContent("Client boundary", "readiness", ["security"], [])), context, CancellationToken.None)).Error,
            "commitment" => (await bus.DispatchAsync(new CreateCommitmentDraft(Tenant, Program,
                Uuid.CreateVersion4(), "commitment", "COM-1", "Commitment statement", "Context", "Source"), context, CancellationToken.None)).Error,
            "mapping" => (await bus.DispatchAsync(new ProposeCriterionNotApplicable(Tenant, Program,
                Uuid.CreateVersion4(), "CC1.1", 0, "Client applicability decision"), context, CancellationToken.None)).Error,
            "overlay" => (await bus.DispatchAsync(new SetCriteriaTextOverlay(Tenant, Uuid.CreateVersion4(),
                "CC1.1", null, new CriteriaTextOverlayContent("Licensed text", "Supplier", "License",
                    new CriteriaOverlayUsageFlags(true, false))), context, CancellationToken.None)).Error,
            "evaluation_plan" => (await bus.DispatchAsync(new DefineControlEvaluationPlan(Tenant, Program,
                Uuid.CreateVersion4(), Uuid.CreateVersion4(), 0, "Review control design", [], true), context, CancellationToken.None)).Error,
            "finding" => (await bus.DispatchAsync(new RaiseFinding(Tenant, Program,
                new FindingSource("manual", null, null, "Client source finding"), "Finding", "Description", "medium",
                "Client scope", RbacIds.Member(Tenant, User), new DateOnly(2027, 1, 1)), context, CancellationToken.None)).Error,
            "readiness" => (await bus.DispatchAsync(new RunReadinessAssessment(Tenant, Program, 0,
                Now), context, CancellationToken.None)).Error,
            "training" => (await bus.DispatchAsync(new DefineTrainingRequirement(Tenant, Program, "TR-1",
                new TrainingRequirementContent("Security training", "Training description", "core_security", null,
                    "manual")), context, CancellationToken.None)).Error,
            _ => throw new InvalidOperationException()
        };

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, error?.Kind);
        Assert.Contains("Attest", error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldPreserveOwnPersonalPolicyAcknowledgementGivenActualAttestHistoryAndOnlyOrdinaryReadGrant()
    {
        // Arrange
        await using var provider = Compose(readOnly: true);
        await SeedProgramAsync(provider);
        var personId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var memberId = RbacIds.Member(Tenant, User);
        var actor = ActorReference.ForMember(memberId, "Client participant");
        await ProgramManagementServices.SeedAsync(provider, new Person(Tenant, personId), person =>
        {
            Assert.True(person.Record("Client participant", null, actor, Now).IsSuccess);
            Assert.Null(person.CorrelateMembership(1, User, actor, Now));
            return Result.Success;
        });
        await ProgramManagementServices.SeedAsync(provider, new PolicyDistributionCampaign(Tenant, campaignId),
            campaign => campaign.Launch(Program,
                new CampaignSubject("policy", Uuid.CreateVersion4(), "POL-1", "Synthetic frozen policy", 1,
                    "synthetic-pinned-policy-hash"), PolicyAudience.CoreSecurity, [], Uuid.CreateVersion4(),
                "synthetic-frozen-roster-hash", new RosterAudience(
                    new Dictionary<Uuid, RosterMatch>
                    {
                        [personId] = new(personId, "Client participant", "employee", "Engineering", new DateOnly(2025, 1, 1)),
                    }, new HashSet<Uuid> { personId }), new DateOnly(2027, 1, 1), null, actor, memberId, Now));
        await AttestAssignmentHistoryFixture.SeedAsync(provider, Tenant, User, revoked: true);
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            new AcknowledgePolicy(Tenant, Program, campaignId, personId, 1, "synthetic-pinned-policy-hash",
                PolicyDistributionCampaign.DefaultAcknowledgementText),
            new RequestDispatchContext(ProgramManagementServices.Actor(User),
                new HttpInvocation("POST", "/synthetic/policy-acknowledgement", "/synthetic/policy-acknowledgement", "synthetic")),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.False(result.Value!.RecordedOnBehalf);
        Assert.Equal(personId.ToString(), result.Value.Performer.Id);
        Assert.Equal(memberId.ToString(), result.Value.Recorder.Id);
    }

    [Theory]
    [InlineData("advisory", true)]
    [InlineData("attest", false)]
    public async Task ShouldPermitProgramManagementGivenNoSameClientActualAttestHistory(string practice,
        bool sameClient)
    {
        // Arrange
        await using var provider = Compose();
        await AttestAssignmentHistoryFixture.SeedAsync(provider, sameClient ? Tenant : Uuid.CreateVersion4(),
            User, revoked: true, practice: practice);
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(
            new CreateProgram(Tenant, "New program", new ProgramPlan(null, null, null, null, null, null)),
            ProgramManagementServices.Actor(User));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    [Fact]
    public async Task ShouldPreserveSharedBoundaryReadGivenActualAttestHistoryAndOrdinaryProgramGrant()
    {
        // Arrange
        await using var provider = Compose();
        await SeedProgramAsync(provider);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var actor = ProgramManagementServices.Actor(User);
        var created = await bus.SendAsync(new CreateBoundary(Tenant, Program,
            new BoundaryContent("Client boundary", "readiness", ["security"], [])), actor);
        Assert.True(created.IsSuccess, created.Error?.Message);
        await ProjectBoundariesAsync(scope.ServiceProvider);
        await AttestAssignmentHistoryFixture.SeedAsync(provider, Tenant, User, revoked: true);

        // Act
        var read = await bus.SendAsync(new GetBoundary(Tenant, created.Value!.BoundaryId), actor);

        // Assert
        Assert.True(read.IsSuccess, read.Error?.Message);
        Assert.Equal("Client boundary", read.Value!.Draft!.Content.Statement);
    }

    static Task SeedProgramAsync(IServiceProvider provider) => ProgramManagementServices.SeedAsync(provider,
        new ComplianceProgram(Tenant, Program), program =>
        {
            Assert.Null(program.Create("Security", new ProgramPlan(null, null, null, null, null, null),
                RbacIds.Member(Tenant, User), "Client administrator", Now));
            return Result.Success;
        });

    static async Task ProjectBoundariesAsync(IServiceProvider services)
    {
        var events = services.GetRequiredService<IDomainEventReader>();
        var projection = services.GetRequiredService<IBoundaryDirectoryProjection>();
        var identity = new CheckpointIdentity("BoundaryDirectoryV2", EventStreamPattern.ForPattern(Tenant.ToString()));
        await using var batch = await projection.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start));
        var cursor = ProjectionCheckpoint.Start.Cursor;
        await foreach (var record in events.ReadAsync(identity.Pattern, cursor, CancellationToken.None))
        {
            await projection.ApplyAsync(record.Event, CancellationToken.None);
            cursor = record.NextCursor;
        }
        await batch.CommitAsync(new ProjectionCheckpoint(cursor));
    }

    static ServiceProvider Compose(bool readOnly = false)
    {
        var services = new ServiceCollection();
        services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance",
        }).Build(), developerAuthentication: true);
        var events = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>(events);
        services.AddSingleton<IKvClient>(new InMemoryKvClient());
        services.AddSingleton<ITenantActivity>(new ActiveTenant());
        services.AddSingleton<ITenantMembershipDirectoryReader>(new Memberships());
        services.AddSingleton<IAccessGrantPermissionAuthorizer>(new PermissionBackedAccessGrantPermissionAuthorizer(
            readOnly ? new ReadOnlyPermissions() : new RecordingPermissionAuthorizer(true)));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    sealed class ReadOnlyPermissions : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(permission == IProgramReadRequest.ReadPermission);
    }

    sealed class Memberships : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult<TenantMembershipView?>(tenantId == Tenant.ToString() && userId == User
                ? new TenantMembershipView(userId, Tenant, "client_personnel") : null);
        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(tenantId == Tenant.ToString() && userId == User);
        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }
}

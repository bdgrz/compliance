using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.Json;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.TechnologyInventory;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Evidence;

public sealed class ArtifactMetadataAccessTests
{
    [Fact]
    public async Task ShouldIssueExactArtifactScopeGivenCurrentAdministratorAndCanonicalArtifact()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var request = fixture.Grant(fixture.ArtifactId, BuiltInRbac.ViewerRoleId(fixture.TenantId));

        // Act
        var result = await fixture.DispatchAsync(request);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var retained = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new AccessGrant(fixture.TenantId, request.GrantId));
        Assert.True(retained.CommittedStreamPosition > 0);
    }

    [Fact]
    public async Task ShouldReadNativeMetadataGivenExplicitArtifactGrant()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        Assert.True((await fixture.DispatchAsync(fixture.Grant(fixture.ArtifactId,
            BuiltInRbac.ViewerRoleId(fixture.TenantId)))).IsSuccess);
        await fixture.ProjectAsync();

        // Act
        var result = await fixture.ReadAsync();

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("restricted", result.Value.Content!.HandlingClass);
        Assert.Equal(EvidenceArtifactStates.PendingInspection, result.Value.State);
        Assert.Equal(new string('a', 64), result.Value.ContentSha256);
    }

    [Fact]
    public async Task ShouldCaptureExactRegistrationIdentityGivenNativeMalwareRelease()
    {
        // Arrange: inspector verdict and rescan rationale are synthetic test facts, not provider qualification.
        await using var fixture = await Fixture.CreateAsync();
        await fixture.AllowAsync();
        await fixture.InspectAsync(EvidenceInspectionOutcome.Malware);
        await ProgramManagementServices.SeedAsync(fixture.Provider, new EvidenceArtifact(fixture.TenantId, fixture.ArtifactId), artifact =>
        {
            Assert.Null(artifact.ReleaseQuarantine("Synthetic clean rescan", ActorReference.ForMember(
                RbacIds.Member(fixture.TenantId, fixture.UserId), "Collector"), Fixture.Now));
            return Result.Success;
        });
        var retained = await fixture.HydrateAsync();

        // Act
        var result = await fixture.ReadAsync();
        await using var scope = fixture.Provider.CreateAsyncScope();
        var captured = await scope.ServiceProvider.GetRequiredService<EvidenceArtifactMetadataRead>()
            .GetCapturedAsync(fixture.TenantId, fixture.ArtifactId, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(EvidenceArtifactStates.Available, result.Value.State);
        Assert.Null(result.Value.StateReason);
        Assert.Equal(3UL, result.Value.SourcePosition);
        Assert.True(captured.IsSuccess, captured.Error?.Message);
        Assert.Equal(retained.Registration!.Metadata.EventId, captured.Value.RegistrationEventId);
        Assert.Equal(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            retained.Registration, ComplianceCoreJsonContext.Default.EvidenceArtifactRegistered))), captured.Value.RegistrationPayloadSha256);
    }

    [Theory]
    [InlineData("public", false)]
    [InlineData("internal", false)]
    [InlineData("confidential", true)]
    [InlineData("restricted", true)]
    public async Task ShouldPreserveClassificationAndNativeFactsGivenExactGrantOverHttpOrMcp(string classification, bool mcp)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(classification: classification);
        await fixture.AllowAsync();
        var before = await fixture.HydrateAsync();

        // Act
        var result = await fixture.ReadAsync(invocation: mcp ? new McpInvocation("artifact-metadata") : null);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(classification, result.Value.Content!.HandlingClass);
        Assert.Equal(new DateOnly(2026, 12, 31), result.Value.Content.PeriodEnd);
        Assert.Equal(RbacIds.Member(fixture.TenantId, fixture.UserId).ToString(), result.Value.Collector.Id);
        Assert.Equal(before.CommittedStreamPosition, result.Value.SourcePosition);
        Assert.Equal(before.CommittedStreamPosition, (await fixture.HydrateAsync()).CommittedStreamPosition);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("organization")]
    [InlineData("program")]
    [InlineData("information_asset")]
    public async Task ShouldHideExistingAndAbsentMetadataGivenNoExactArtifactGrant(string scope)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        if (scope != "none")
        {
            AccessGrantScope grantScope;
            if (scope == "organization")
                grantScope = new(AccessGrantScopeKind.Organization, fixture.TenantId);
            else if (scope == "program")
            {
                var program = Uuid.CreateVersion4();
                await ProgramManagementServices.SeedAsync(fixture.Provider, new ComplianceProgram(fixture.TenantId, program), item =>
                {
                    Assert.Null(item.Create("Program", new ProgramPlan(null, null, null, null, null, null), fixture.UserId, "Administrator", Fixture.Now));
                    return Result.Success;
                });
                grantScope = new(AccessGrantScopeKind.Program, program);
            }
            else
            {
                // Real different resource type deliberately shares the UUID; its exact grant must not authorize this artifact.
                await ProgramManagementServices.SeedAsync(fixture.Provider, new InformationAsset(fixture.TenantId, fixture.ArtifactId), item =>
                    item.Record(new InformationAssetContent("Different resource", "public", "M0-D16", fixture.UserId, null, "active"),
                        ActorReference.ForMember(RbacIds.Member(fixture.TenantId, fixture.UserId), "Administrator"), Fixture.Now));
                grantScope = new(AccessGrantScopeKind.SharedResource, fixture.ArtifactId, TechnologyInventoryResourceTypes.InformationAsset);
            }
            Assert.True((await fixture.DispatchAsync(fixture.Grant(fixture.ArtifactId, BuiltInRbac.ViewerRoleId(fixture.TenantId)) with
            {
                Proposal = fixture.Grant(fixture.ArtifactId, BuiltInRbac.ViewerRoleId(fixture.TenantId)).Proposal with { Scope = grantScope }
            })).IsSuccess);
            await fixture.ProjectAsync();
        }

        // Act
        var existing = await fixture.ReadAsync();
        var absent = await fixture.ReadAsync(artifactId: Uuid.CreateVersion4());

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, existing.Error!.Kind);
        Assert.Equal(existing.Error.Message, absent.Error!.Message);
        Assert.Equal(RequestErrorKind.NotFound, absent.Error.Kind);
    }

    [Theory]
    [InlineData("revoked")]
    [InlineData("permission_removed")]
    [InlineData("suspended")]
    [InlineData("deprovisioned")]
    [InlineData("inactive_tenant")]
    public async Task ShouldRefuseStaleProjectedAuthorityGivenCurrentSourceRevocation(string change)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var grant = await fixture.AllowAsync();
        Assert.True((await fixture.ReadAsync()).IsSuccess);
        await fixture.RevokeAuthorityAsync(change, grant);

        // Act
        var result = await fixture.ReadAsync(invocation: new McpInvocation("artifact-metadata"));

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error!.Kind);
    }

    [Fact]
    public async Task ShouldRefuseBorrowedCurrentMembershipGivenForeignCanonicalUserInRetainedSource()
    {
        // Arrange: retain a valid-shaped wrong-identity source while the genuine membership projection remains current.
        await using var fixture = await Fixture.CreateAsync();
        await fixture.AllowAsync();
        var member = await ProgramManagementServices.HydrateAsync(fixture.Provider, new Member(fixture.TenantId, fixture.UserId));
        var malformed = new MemberRegistered(fixture.TenantId, member.Id, Uuid.CreateVersion4(),
            "client_personnel", member.MembershipEpisodeId);
        malformed.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), member.Id,
            member.CommittedStreamPosition + 1, Fixture.Now, null, null, false));
        await fixture.Provider.GetRequiredService<IEventStore>().AppendAsync(member.Stream,
            member.CommittedStreamPosition, [malformed], CancellationToken.None);

        // Act
        var result = await fixture.ReadAsync();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, result.Error!.Kind);
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("system")]
    [InlineData("nonmember")]
    [InlineData("foreign_tenant")]
    [InlineData("other_artifact")]
    public async Task ShouldRefuseBorrowedArtifactAuthorityGivenActorOrResourceMismatch(string change)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await fixture.AllowAsync();
        var actor = change switch
        {
            "anonymous" => RequestActor.Anonymous,
            "system" => RequestActor.System,
            "nonmember" => ProgramManagementServices.Actor(Uuid.CreateVersion4()),
            _ => ProgramManagementServices.Actor(fixture.UserId)
        };

        // Act
        var result = await fixture.ReadAsync(tenantId: change == "foreign_tenant" ? Uuid.CreateVersion4() : null,
            artifactId: change == "other_artifact" ? Uuid.CreateVersion4() : null, actor: actor);

        // Assert
        Assert.Equal(change is "anonymous" or "system" ? RequestErrorKind.Unauthorized : RequestErrorKind.NotFound, result.Error!.Kind);
    }

    [Theory]
    [InlineData(EvidenceInspectionOutcome.SecretDetected, "secret_detected")]
    [InlineData(EvidenceInspectionOutcome.Invalid, "invalid")]
    public async Task ShouldExposeOnlyPermittedTombstoneGivenRejectedUpload(EvidenceInspectionOutcome outcome, string reason)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await fixture.AllowAsync();
        // Explicit synthetic inspector verdict exercises canonical source behavior, not a production scanner.
        await fixture.InspectAsync(outcome);

        // Act
        var result = await fixture.ReadAsync();
        var json = JsonSerializer.Serialize(result.Value, ComplianceCoreJsonContext.Default.EvidenceArtifactMetadataView);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Content);
        Assert.Equal(EvidenceArtifactStates.Rejected, result.Value.State);
        Assert.Equal(reason, result.Value.StateReason);
        Assert.Equal(42, result.Value.ContentLength);
        Assert.Equal(Fixture.Now, result.Value.RegisteredAt);
        Assert.DoesNotContain("Restricted native evidence", json);
        Assert.DoesNotContain("Source description", json);
        Assert.DoesNotContain("Native source", json);
        Assert.DoesNotContain("period_start", json);
    }

    [Theory]
    [InlineData(EvidenceInspectionOutcome.Clean, "available")]
    [InlineData(EvidenceInspectionOutcome.Malware, "quarantined")]
    public async Task ShouldReportCurrentAvailabilityWithoutIssuingDeliveryGivenCanonicalInspection(EvidenceInspectionOutcome outcome, string state)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await fixture.AllowAsync();
        await fixture.InspectAsync(outcome);

        // Act
        var result = await fixture.ReadAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(state, result.Value.State);
        Assert.NotNull(result.Value.Content);
        Assert.Equal(2UL, result.Value.SourcePosition);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("grant")]
    [InlineData("member")]
    public async Task ShouldRefuseOldMetadataGivenSourceOrAuthorityChangeDuringRead(string change)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var grant = await fixture.AllowAsync();
        fixture.Faults.OnArtifactRead = change == "source" ? () => fixture.InspectAsync(EvidenceInspectionOutcome.Malware) :
            () => fixture.RevokeAuthorityAsync(change == "grant" ? "revoked" : "deprovisioned", grant);

        // Act
        var result = await fixture.ReadAsync();

        // Assert
        Assert.Equal(change == "source" ? RequestErrorKind.Conflict : RequestErrorKind.NotFound, result.Error!.Kind);
        if (change == "source")
            Assert.True(result.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldRefuseMetadataGivenIncompleteAuthoritativeEnumeration()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await fixture.AllowAsync();
        fixture.Faults.HideArtifactEvents = true;

        // Act
        var result = await fixture.ReadAsync();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.True(result.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldBackfillPermissionWithoutGrantsGivenCompletedLegacyCatalogCheckpoint()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(legacy: true);
        await fixture.RunReactorAsync("BuiltInRoleCatalogV1");
        await fixture.ProjectAsync();
        var before = await fixture.TenantEventsAsync();
        var permission = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new RolePermission(fixture.TenantId, BuiltInRbac.ViewerRoleId(fixture.TenantId), RbacPermissions.EvidenceArtifactRead));
        Assert.False(permission.IsAssigned);

        // Act
        await fixture.RunReactorAsync("EvidenceArtifactReadPermissionV1");
        await fixture.ProjectAsync();
        var after = await fixture.TenantEventsAsync();
        var result = await fixture.ReadAsync();
        await fixture.RunReactorAsync("EvidenceArtifactReadPermissionV1");

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error!.Kind);
        Assert.Equal(before.OfType<AccessGrantIssued>().Count(), after.OfType<AccessGrantIssued>().Count());
        Assert.Equal(before.OfType<TeamMemberAssigned>().Count(), after.OfType<TeamMemberAssigned>().Count());
        Assert.Equal(before.OfType<RoleDefined>().Count(), after.OfType<RoleDefined>().Count());
        Assert.Equal(before.Count + 1, after.Count);
        Assert.Equal(after.Count, (await fixture.TenantEventsAsync()).Count);
        Assert.True((await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new RolePermission(fixture.TenantId, BuiltInRbac.ViewerRoleId(fixture.TenantId), RbacPermissions.EvidenceArtifactRead))).IsAssigned);
        Assert.True((await fixture.DispatchAsync(fixture.Grant(fixture.ArtifactId, BuiltInRbac.ViewerRoleId(fixture.TenantId)))).IsSuccess);
        await fixture.ProjectAsync();
        Assert.True((await fixture.ReadAsync()).IsSuccess);
    }

    [Theory]
    [InlineData("event_id")]
    [InlineData("aggregate_id")]
    public async Task ShouldRefusePrivateRegistrationCapsuleGivenSameBusinessPayloadWithChangedSourceIdentity(string change)
    {
        // Arrange: isolated enumeration fault preserves stream/offset/payload while changing retained envelope identity.
        await using var fixture = await Fixture.CreateAsync();
        await fixture.AllowAsync();
        fixture.Faults.RegistrationIdentity = change;

        // Act
        var read = await fixture.ReadAsync();
        await using var scope = fixture.Provider.CreateAsyncScope();
        var captured = await scope.ServiceProvider.GetRequiredService<EvidenceArtifactMetadataRead>()
            .GetCapturedAsync(fixture.TenantId, fixture.ArtifactId, CancellationToken.None);

        // Assert
        Assert.False(read.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, read.Error!.Kind);
        Assert.False(captured.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, captured.Error!.Kind);
    }

    [Theory]
    [InlineData("rejected_release")]
    [InlineData("release_pending")]
    [InlineData("duplicate_registration")]
    [InlineData("second_inspection")]
    [InlineData("unknown_reason")]
    [InlineData("foreign_inspection")]
    public async Task ShouldRefuseMetadataAndNewGrantWithoutAppendGivenImpossibleRetainedSource(string change)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await fixture.AllowAsync();
        if (change == "rejected_release")
            await fixture.InspectAsync(EvidenceInspectionOutcome.SecretDetected);
        else if (change == "second_inspection")
            await fixture.InspectAsync(EvidenceInspectionOutcome.Clean);
        var source = await fixture.HydrateAsync();
        DomainEvent malformed = change switch
        {
            "rejected_release" or "release_pending" => new EvidenceArtifactQuarantineReleased(fixture.TenantId,
                fixture.ArtifactId, "Cannot change a rejected or pending source", source.Registration!.Collector, Fixture.Now),
            "duplicate_registration" => new EvidenceArtifactRegistered(fixture.TenantId, fixture.ArtifactId,
                source.Content!, source.ContentSha256!, source.ContentLength!.Value, source.Registration!.Collector, Fixture.Now),
            _ => new EvidenceArtifactInspected(change == "foreign_inspection" ? Uuid.CreateVersion4() : fixture.TenantId,
                fixture.ArtifactId, EvidenceArtifactStates.Rejected, change == "unknown_reason" ? "not_a_verdict" :
                    EvidenceArtifactStates.SecretDetected, Fixture.Now)
        };
        malformed.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), fixture.ArtifactId,
            source.CommittedStreamPosition + 1, Fixture.Now, null, null, false));
        await fixture.Provider.GetRequiredService<IEventStore>().AppendAsync(source.Stream,
            source.CommittedStreamPosition, [malformed], CancellationToken.None);
        var newGrant = fixture.Grant(fixture.ArtifactId, BuiltInRbac.ViewerRoleId(fixture.TenantId));

        // Act
        var read = await fixture.ReadAsync();
        var issue = await fixture.DispatchAsync(newGrant);
        var retained = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new AccessGrant(fixture.TenantId, newGrant.GrantId));

        // Assert
        Assert.False(read.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, read.Error!.Kind);
        Assert.False(issue.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, issue.Error!.Kind);
        Assert.Equal(0UL, retained.CommittedStreamPosition);
    }

    internal sealed class Fixture : IAsyncDisposable
    {
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public Uuid ArtifactId { get; } = Uuid.CreateVersion4();
        public ServiceProvider Provider { get; }
        public Faults Faults { get; } = new();
        public static DateTimeOffset Now { get; } = new(2026, 10, 8, 18, 0, 0, TimeSpan.Zero);

        Fixture()
        {
            var services = new ServiceCollection();
            services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance"
            }).Build(), developerAuthentication: true);
            var events = new InMemoryEventStore();
            services.AddSingleton<IEventStore>(events);
            services.AddSingleton<IDomainEventReader>(new FaultingReader(events, Faults));
            services.AddSingleton<IKvClient>(new InMemoryKvClient());
            services.AddSingleton<TimeProvider>(new Clock());
            Provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        }

        public static async Task<Fixture> CreateAsync(string classification = "restricted", bool legacy = false)
        {
            var fixture = new Fixture();
            await ProgramManagementServices.SeedAsync(fixture.Provider, new Tenant(fixture.TenantId), tenant =>
            {
                Assert.True(tenant.Register(fixture.UserId, "Artifact test client", "artifact-test").IsSuccess);
                return tenant.ConfirmSlug("artifact-test");
            });
            if (legacy)
                await fixture.SeedLegacyRbacAsync();
            else
                await fixture.RunReactorAsync("TenantRbacBootstrap");
            await fixture.ProjectAsync();
            await ProgramManagementServices.SeedAsync(fixture.Provider,
                new EvidenceArtifact(fixture.TenantId, fixture.ArtifactId), artifact => artifact.Register(
                    new EvidenceArtifactContent("Restricted native evidence", "Source description", "manual", "Native source",
                        Now, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), classification),
                    new string('a', 64), 42, ActorReference.ForMember(RbacIds.Member(fixture.TenantId, fixture.UserId), "Collector"), Now));
            return fixture;
        }

        public GrantAccess Grant(Uuid artifactId, Uuid roleId) => new(TenantId, Uuid.CreateVersion4(),
            new AccessGrantProposal(new AccessGrantPrincipal(AccessGrantPrincipalKind.Member, RbacIds.Member(TenantId, UserId)),
                roleId, new AccessGrantScope(AccessGrantScopeKind.SharedResource, artifactId, "evidence_artifact"),
                new AccessGrantSource("manual", "Explicit artifact grant"), Now, null));

        public async Task<Result> DispatchAsync(IRequest request)
        {
            await using var scope = Provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
                new RequestDispatchContext(ProgramManagementServices.Actor(UserId),
                    new HttpInvocation("POST", "/test", "/test", "test")));
        }

        public async Task<Result<EvidenceArtifactMetadataView>> ReadAsync(Uuid? tenantId = null, Uuid? artifactId = null,
            RequestInvocation? invocation = null, ClaimsPrincipal? actor = null)
        {
            await using var scope = Provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
                new GetEvidenceArtifactMetadata(tenantId ?? TenantId, artifactId ?? ArtifactId),
                new RequestDispatchContext(actor ?? ProgramManagementServices.Actor(UserId),
                    invocation ?? new HttpInvocation("GET", "/test", "/test", "test")));
        }

        public Task<EvidenceArtifact> HydrateAsync() => ProgramManagementServices.HydrateAsync(Provider,
            new EvidenceArtifact(TenantId, ArtifactId));

        public async Task<Uuid> AllowAsync()
        {
            var request = Grant(ArtifactId, BuiltInRbac.ViewerRoleId(TenantId));
            var result = await DispatchAsync(request);
            Assert.True(result.IsSuccess, result.Error?.Message);
            await ProjectAsync();
            return request.GrantId;
        }

        public Task InspectAsync(EvidenceInspectionOutcome outcome) => ProgramManagementServices.SeedAsync(Provider,
            new EvidenceArtifact(TenantId, ArtifactId), artifact =>
            {
                Assert.Null(artifact.RecordInspection(outcome, Now));
                return Result.Success;
            });

        public async Task RevokeAuthorityAsync(string change, Uuid grant)
        {
            if (change == "revoked")
                Assert.True((await DispatchAsync(new RevokeAccessGrant(TenantId, grant))).IsSuccess);
            else if (change == "permission_removed")
                await ProgramManagementServices.SeedAsync(Provider,
                    new RolePermission(TenantId, BuiltInRbac.ViewerRoleId(TenantId), RbacPermissions.EvidenceArtifactRead), item => item.Remove());
            else if (change == "inactive_tenant")
                await ProgramManagementServices.SeedAsync(Provider, new Tenant(TenantId), item => item.Suspend(UserId, "Paused client", Now));
            else
                await ProgramManagementServices.SeedAsync(Provider, new Member(TenantId, UserId), member => change == "suspended"
                    ? member.Suspend(RbacIds.Member(TenantId, UserId), "Administrator", Now, "Suspended member")
                    : member.Deprovision(RbacIds.Member(TenantId, UserId), "Administrator", Now, "Removed member"));
        }

        public async Task<List<DomainEvent>> TenantEventsAsync()
        {
            await using var scope = Provider.CreateAsyncScope();
            var result = new List<DomainEvent>();
            await foreach (var record in scope.ServiceProvider.GetRequiredService<IDomainEventReader>()
                               .ReadAsync(EventStreamPattern.ForPattern(TenantId.ToString()), EventCursor.Start, CancellationToken.None))
                result.Add(record.Event);
            return result;
        }

        async Task SeedLegacyRbacAsync()
        {
            // Canonical historical fixed-catalog source fixtures, before this permission existed.
            await ProgramManagementServices.SeedAsync(Provider, new Member(TenantId, UserId), member => member.Register());
            var member = await ProgramManagementServices.HydrateAsync(Provider, new Member(TenantId, UserId));
            foreach (var role in new[] { BuiltInRbac.TenantAdministrationRoleId(TenantId),
                         BuiltInRbac.ComplianceManagementRoleId(TenantId), BuiltInRbac.ComplianceParticipationRoleId(TenantId) })
                await ProgramManagementServices.SeedAsync(Provider, new Role(TenantId, role), item => item.Define("Historic fixed role"));
            var team = BuiltInRbac.AdministratorsTeamId(TenantId);
            var administrator = BuiltInRbac.TenantAdministrationRoleId(TenantId);
            await ProgramManagementServices.SeedAsync(Provider, new Team(TenantId, team), item => item.Define("Administrators"));
            await ProgramManagementServices.SeedAsync(Provider, new TeamRole(TenantId, team, administrator), item => item.Assign());
            await ProgramManagementServices.SeedAsync(Provider, new TeamMember(TenantId, team, RbacIds.Member(TenantId, UserId)), item =>
                item.Assign(member.MembershipEpisodeId));
            foreach (var permission in new[] { RbacPermissions.TenantAccess, RbacPermissions.TenantRbacManage })
                await ProgramManagementServices.SeedAsync(Provider, new RolePermission(TenantId, administrator, permission), item => item.Assign());
        }

        public async Task RunReactorAsync(string name)
        {
            await using var scope = Provider.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var registration = Assert.Single(services.GetServices<WorkloadRegistration>(), item => item.Name == name);
            var reactor = (Reactor)services.GetRequiredService(registration.ComponentType);
            var checkpoints = services.GetRequiredService<IProjectionCheckpointStore>();
            var checkpoint = await checkpoints.LoadAsync(new CheckpointIdentity(reactor.Name, reactor.Pattern));
            var runner = new ReactorRunner(services.GetRequiredService<IDomainEventReader>(),
                services.GetRequiredService<IReactorPrincipalProvider>(), services.GetRequiredService<TimeProvider>());
            while (true)
            {
                var next = await runner.RunAsync(reactor, checkpoint);
                if (next == checkpoint)
                    break;
                checkpoint = next;
            }
        }

        public async Task ProjectAsync()
        {
            foreach (var name in new[] { "TenantMembership", "PermissionProjection", "RoleDirectory", "RolePermissionDirectory", "AccessGrantsV1" })
            {
                await using var scope = Provider.CreateAsyncScope();
                var services = scope.ServiceProvider;
                var registration = Assert.Single(services.GetServices<WorkloadRegistration>(), item => item.Name == name);
                var projector = (Projector)services.GetRequiredService(registration.ComponentType);
                await new ProjectorScenario(new TenantId(TenantId.ToString())).RunAsync(projector);
                var store = name switch
                {
                    "TenantMembership" => (IProjectionStore)services.GetRequiredService<ITenantMembershipDirectoryProjection>(),
                    "PermissionProjection" => services.GetRequiredService<FitzPermissionAuthorizer>(),
                    "RoleDirectory" => (IProjectionStore)services.GetRequiredService<IRoleDirectoryProjection>(),
                    "RolePermissionDirectory" => (IProjectionStore)services.GetRequiredService<IRolePermissionDirectoryProjection>(),
                    _ => (IProjectionStore)services.GetRequiredService<IAccessGrantProjection>()
                };
                var checkpoint = await store.LoadCheckpointAsync(new CheckpointIdentity(projector.Name, projector.Pattern));
                var runner = new ProjectorRunner(services.GetRequiredService<IDomainEventReader>());
                while (true)
                {
                    var next = await runner.RunAsync(projector, checkpoint);
                    if (next == checkpoint)
                        break;
                    checkpoint = next;
                }
            }
        }

        public ValueTask DisposeAsync() => Provider.DisposeAsync();
    }

    internal sealed class Faults
    {
        public Func<Task>? OnArtifactRead { get; set; }
        public bool HideArtifactEvents { get; set; }
        public string? RegistrationIdentity { get; set; }
    }

    sealed class FaultingReader(IDomainEventReader inner, Faults faults) : IDomainEventReader
    {
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream, ulong offset,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var record in inner.ReadAsync(stream, offset, ct))
            {
                if (stream.Area == EvidenceStreams.Area)
                {
                    if (faults.OnArtifactRead is { } mutate)
                    {
                        faults.OnArtifactRead = null;
                        await mutate();
                    }
                    if (faults.HideArtifactEvents)
                        yield break;
                    if (record.Event is EvidenceArtifactRegistered registration && faults.RegistrationIdentity is { } change)
                    {
                        var copy = new EvidenceArtifactRegistered(registration.TenantId, registration.ArtifactId, registration.Content,
                            registration.ContentSha256, registration.ContentLength, registration.Collector, registration.RegisteredAt);
                        copy.AttachMetadata(registration.Metadata with
                        {
                            EventId = change == "event_id" ? Uuid.CreateVersion4() : registration.Metadata.EventId,
                            AggregateId = change == "aggregate_id" ? Uuid.CreateVersion4() : registration.Metadata.AggregateId
                        });
                        yield return record with { Event = copy };
                        continue;
                    }
                }
                yield return record;
            }
        }

        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            CancellationToken ct = default) => inner.ReadAsync(pattern, cursor, ct);
    }

    sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Fixture.Now;
    }
}

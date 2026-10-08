using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Retention;
using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Retention;

public sealed class ArtifactRetentionHandlerTests
{
    [Fact]
    public async Task ShouldPersistNativeEvidencePeriodAndHoldsGivenSourceBoundPersonalDecision()
    {
        // Arrange
        await using var fixture = new Fixture();
        var id = await fixture.EvidenceAsync();
        var context = new Cntryl.Portia.RequestContext<RecordArtifactRetentionBasis>(
            new(fixture.Tenant, "evidence_artifact", id, Fixture.Sha, 0, "Native source verified"), fixture.Http);
        var handler = new RecordArtifactRetentionBasisHandler(fixture.Mutation);

        // Act
        var recorded = await handler.HandleAsync(context, CancellationToken.None);
        var replay = await handler.HandleAsync(context, CancellationToken.None);
        var read = await fixture.Read.GetAsync(fixture.Tenant, "evidence_artifact", id, 2, CancellationToken.None);
        var aggregate = await fixture.Reader.HydrateAsync(new ArtifactRetention(fixture.Tenant, "evidence_artifact", id));

        // Assert
        Assert.True(recorded.IsSuccess);
        Assert.True(replay.IsSuccess);
        Assert.True(read.IsSuccess);
        Assert.Equal(new DateOnly(2019, 12, 31), read.Value.PeriodEnd);
        Assert.Equal(2UL, aggregate.CommittedStreamPosition);
        Assert.Contains("storage_hold_enforcement_pending", read.Value.Blockers);
        Assert.False(read.Value.DispositionAllowed);
    }

    [Theory]
    [InlineData("digest")]
    [InlineData("period")]
    [InlineData("tenant")]
    [InlineData("system")]
    public async Task ShouldAppendNoPolicyGivenUnverifiedSourceOrActor(string change)
    {
        // Arrange
        await using var fixture = new Fixture();
        var id = await fixture.EvidenceAsync();
        var request = new RecordArtifactRetentionBasis(change == "tenant" ? Uuid.CreateVersion4() : fixture.Tenant,
            "evidence_artifact", id, change == "digest" ? new string('b', 64) : Fixture.Sha, 0,
            "Verified", change == "period" ? new DateOnly(2020, 1, 1) : null,
            change == "period" ? new DateOnly(2020, 12, 31) : null);

        // Act
        var result = await new RecordArtifactRetentionBasisHandler(fixture.Mutation).HandleAsync(
            new Cntryl.Portia.RequestContext<RecordArtifactRetentionBasis>(request, change == "system" ? RequestActor.System : fixture.Actor),
            CancellationToken.None);
        var aggregate = await fixture.Reader.HydrateAsync(new ArtifactRetention(fixture.Tenant, "evidence_artifact", id));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(0, aggregate.Revision);
        Assert.Equal(0UL, aggregate.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldExposeAdminConsumerHookWithoutInferredPeriodGivenRetainedImport()
    {
        // Arrange
        await using var fixture = new Fixture();
        var batch = await fixture.ImportAsync();

        // Act
        var missing = await new GetApplicationImportRetentionHandler(fixture.Read).HandleAsync(
            new Cntryl.Portia.RequestContext<GetApplicationImportRetention>(new(fixture.Tenant, batch), fixture.Http), CancellationToken.None);
        var periodless = await new RecordArtifactRetentionBasisHandler(fixture.Mutation).HandleAsync(
            new Cntryl.Portia.RequestContext<RecordArtifactRetentionBasis>(new(fixture.Tenant, "application_import", batch,
                missing.Value.Source.ContentSha256, 0, "Staged now"), fixture.Http), CancellationToken.None);
        var explicitBasis = await new RecordArtifactRetentionBasisHandler(fixture.Mutation).HandleAsync(
            new Cntryl.Portia.RequestContext<RecordArtifactRetentionBasis>(new(fixture.Tenant, "application_import", batch,
                missing.Value.Source.ContentSha256, 0, "Period verified", new(2019, 1, 1), new(2019, 12, 31)), fixture.Http), CancellationToken.None);
        var read = await fixture.Read.GetAsync(fixture.Tenant, "application_import", batch, 2, CancellationToken.None);

        // Assert
        Assert.True(missing.IsSuccess);
        Assert.Equal(0, missing.Value.Revision);
        Assert.Null(missing.Value.PeriodEnd);
        Assert.Contains("supported_period_unrecorded", missing.Value.Blockers);
        Assert.False(periodless.IsSuccess);
        Assert.True(explicitBasis.IsSuccess);
        Assert.Equal(new DateOnly(2026, 12, 31), read.Value.RetainsThrough);
    }

    [Fact]
    public async Task ShouldFencePagingAndReleaseHistoryGivenConcurrentAdminDecision()
    {
        // Arrange
        await using var fixture = new Fixture();
        var id = await fixture.EvidenceAsync();
        var holds = Enumerable.Range(0, 3).Select(_ => Uuid.CreateVersion4()).ToArray();
        var place = new PlaceArtifactLegalHoldHandler(fixture.Mutation);
        for (var index = 0; index < holds.Length; index++)
            Assert.True((await place.HandleAsync(new Cntryl.Portia.RequestContext<PlaceArtifactLegalHold>(new(fixture.Tenant,
                "evidence_artifact", id, Fixture.Sha, holds[index], index == 0 ? 0 : index + 1, "Preserve"), fixture.Http),
                CancellationToken.None)).IsSuccess);
        var list = new ListArtifactLegalHoldsHandler(fixture.Reader);
        var request = new ListArtifactLegalHolds(fixture.Tenant, "evidence_artifact", id, 1);
        var page = await list.HandleAsync(new Cntryl.Portia.RequestContext<ListArtifactLegalHolds>(request, fixture.Http), CancellationToken.None);

        // Act
        var release = await new ReleaseArtifactLegalHoldHandler(fixture.Mutation).HandleAsync(
            new Cntryl.Portia.RequestContext<ReleaseArtifactLegalHold>(new(fixture.Tenant, "evidence_artifact", id, Fixture.Sha,
                holds[0], 4, "Matter closed"), fixture.Http), CancellationToken.None);
        var stale = await list.HandleAsync(new Cntryl.Portia.RequestContext<ListArtifactLegalHolds>(request with { Cursor = page.Value.NextCursor }, fixture.Http), CancellationToken.None);
        var foreign = await list.HandleAsync(new Cntryl.Portia.RequestContext<ListArtifactLegalHolds>(request with { SourceId = Uuid.CreateVersion4(), Cursor = page.Value.NextCursor }, fixture.Http), CancellationToken.None);
        var all = await list.HandleAsync(new Cntryl.Portia.RequestContext<ListArtifactLegalHolds>(request with { Limit = 200 }, fixture.Http), CancellationToken.None);

        // Assert
        Assert.True(release.IsSuccess);
        Assert.Single(page.Value.Items);
        Assert.False(stale.IsSuccess);
        Assert.False(foreign.IsSuccess);
        Assert.Equal(3, all.Value.Items.Count);
        Assert.NotNull(Assert.Single(all.Value.Items, hold => hold.HoldId == holds[0]).ReleasedAt);
    }

    [Theory]
    [InlineData("basis", true)]
    [InlineData("place", true)]
    [InlineData("release", true)]
    [InlineData("get", true)]
    [InlineData("list", true)]
    [InlineData("import", true)]
    [InlineData("import", false)]
    public async Task ShouldRequireCanonicalCurrentAdminGrantGivenRetentionOperation(string operation, bool allowed)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var source = Uuid.CreateVersion4();
        IArtifactRetentionAdminRequest request = operation switch
        {
            "basis" => new RecordArtifactRetentionBasis(tenant, "application_import", source, Fixture.Sha, 0, "Verified"),
            "place" => new PlaceArtifactLegalHold(tenant, "application_import", source, Fixture.Sha, source, 0, "Preserve"),
            "release" => new ReleaseArtifactLegalHold(tenant, "application_import", source, Fixture.Sha, source, 0, "Release"),
            "get" => new GetArtifactRetention(tenant, "application_import", source),
            "list" => new ListArtifactLegalHolds(tenant, "application_import", source),
            _ => new GetApplicationImportRetention(tenant, source),
        };
        var permissions = new RecordingPermissionAuthorizer(allowed);
        var authorizer = new ArtifactRetentionAdminAuthorizer(new FixedMembershipDirectory(true), new ActiveTenant(), permissions);

        // Act
        var result = await authorizer.AuthorizeAsync(new Cntryl.Portia.RequestContext<IArtifactRetentionAdminRequest>(request,
            BdgrzActor(user)), CancellationToken.None);

        // Assert
        Assert.Equal(allowed, result.IsSuccess);
        Assert.Equal(RbacPermissions.TenantRbacManage, Assert.Single(permissions.Permissions));
        Assert.Equal(RbacIds.Member(tenant, user), Assert.Single(permissions.MemberIds));
    }

    [Fact]
    public async Task ShouldRefuseCrossTenantSourceEventGivenCorrectPhysicalStreamAddress()
    {
        // Arrange
        await using var fixture = new Fixture();
        var id = Uuid.CreateVersion4();
        var corrupted = new InjectedEvidence(fixture.Tenant, id, new(Uuid.CreateVersion4(), id,
            new("Evidence", null, "export", "manual", DateTimeOffset.UtcNow, new(2019, 1, 1), new(2019, 12, 31), "confidential"),
            Fixture.Sha, 100, ActorReference.ForMember(Uuid.CreateVersion4(), "Collector"), DateTimeOffset.UtcNow));
        await fixture.Writer.SaveAsync(corrupted,
            new Cntryl.Portia.RequestContext<GetArtifactRetention>(new(fixture.Tenant, "evidence_artifact", id), fixture.Http));

        // Act
        var result = await fixture.Read.GetAsync(fixture.Tenant, "evidence_artifact", id, null, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
    }

    sealed class InjectedEvidence : Aggregate
    {
        public InjectedEvidence(Uuid tenant, Uuid id, EvidenceArtifactRegistered ev)
            : base(id, EvidenceStreams.Address(tenant, id))
        {
            On<EvidenceArtifactRegistered>(_ => { });
            RaiseEvent(ev);
        }
    }

    [Theory]
    [InlineData(false, false, false, "client_personnel", true)]
    [InlineData(true, true, false, "client_personnel", true)]
    [InlineData(true, false, true, "client_personnel", true)]
    [InlineData(true, false, false, "firm_staff", true)]
    [InlineData(true, false, false, "client_personnel", false)]
    public async Task ShouldDenyRetentionMetadataGivenIneligibleCurrentActor(bool member, bool suspended,
        bool deprovisioned, string affiliation, bool active)
    {
        // Arrange
        var permissions = new RecordingPermissionAuthorizer(true);
        var authorizer = new ArtifactRetentionAdminAuthorizer(new FixedMembershipDirectory(member, affiliation,
            suspended, deprovisioned), new TenantActivity(active), permissions);

        // Act
        var result = await authorizer.AuthorizeAsync(new Cntryl.Portia.RequestContext<IArtifactRetentionAdminRequest>(
            new GetApplicationImportRetention(Uuid.CreateVersion4(), Uuid.CreateVersion4()),
            BdgrzActor(Uuid.CreateVersion4())), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(permissions.Permissions);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("policy")]
    public async Task ShouldRejectMixedAssessmentGivenConcurrentSourceOrPolicyAppend(string change)
    {
        // Arrange
        await using var fixture = new Fixture();
        var id = await fixture.EvidenceAsync();
        var reader = new RacingReader(fixture.Reader, async () =>
        {
            if (change == "policy")
                Assert.True((await new PlaceArtifactLegalHoldHandler(fixture.Mutation).HandleAsync(
                    new Cntryl.Portia.RequestContext<PlaceArtifactLegalHold>(new(fixture.Tenant, "evidence_artifact", id,
                        Fixture.Sha, Uuid.CreateVersion4(), 0, "Concurrent hold"), fixture.Http), CancellationToken.None)).IsSuccess);
            else
            {
                var artifact = await fixture.Reader.HydrateAsync(new EvidenceArtifact(fixture.Tenant, id));
                Assert.Null(artifact.RecordInspection(EvidenceInspectionOutcome.Clean, DateTimeOffset.UtcNow));
                await fixture.Writer.SaveAsync(artifact,
                    new Cntryl.Portia.RequestContext<GetArtifactRetention>(new(fixture.Tenant, "evidence_artifact", id), fixture.Http));
            }
        }, change == "source");

        // Act
        var result = await new ArtifactRetentionRead(reader, TimeProvider.System).GetAsync(fixture.Tenant,
            "evidence_artifact", id, null, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(Assert.IsType<RequestError>(result.Error).IsTransient);
    }

    sealed class RacingReader(IAggregateReader inner, Func<Task> race, bool source) : IAggregateReader
    {
        bool _changed;
        public async ValueTask<T> HydrateAsync<T>(T aggregate, CancellationToken ct = default) where T : Aggregate
        {
            var loaded = await inner.HydrateAsync(aggregate, ct);
            if (!_changed && (source ? aggregate is EvidenceArtifact : aggregate is ArtifactRetention))
            {
                _changed = true;
                await race();
            }
            return loaded;
        }
    }

    sealed class TenantActivity(bool active) : ITenantActivity
    {
        public ValueTask<bool> IsActiveAsync(Uuid tenantId, CancellationToken ct = default) => ValueTask.FromResult(active);
    }

    [Fact]
    public async Task ShouldCommitOneDecisionGivenCompetingExpectedRetentionRevision()
    {
        // Arrange
        await using var fixture = new Fixture();
        var id = await fixture.EvidenceAsync();
        var handler = new PlaceArtifactLegalHoldHandler(fixture.Mutation);
        var first = new Cntryl.Portia.RequestContext<PlaceArtifactLegalHold>(new(fixture.Tenant, "evidence_artifact", id,
            Fixture.Sha, Uuid.CreateVersion4(), 0, "Matter A"), fixture.Http);
        var second = new Cntryl.Portia.RequestContext<PlaceArtifactLegalHold>(new(fixture.Tenant, "evidence_artifact", id,
            Fixture.Sha, Uuid.CreateVersion4(), 0, "Matter B"), fixture.Http);

        // Act
        var results = await Task.WhenAll(handler.HandleAsync(first, CancellationToken.None).AsTask(),
            handler.HandleAsync(second, CancellationToken.None).AsTask());
        var retention = await fixture.Reader.HydrateAsync(new ArtifactRetention(fixture.Tenant, "evidence_artifact", id));

        // Assert
        Assert.Single(results, result => result.IsSuccess);
        Assert.Single(results, result => !result.IsSuccess);
        Assert.Single(retention.GetLegalHolds());
        Assert.Equal(2, retention.Revision);
        Assert.Equal(2UL, retention.CommittedStreamPosition);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRejectReusedMutableReaderRaceGivenAssessmentOrHistory(bool history)
    {
        // Arrange
        await using var fixture = new Fixture();
        var id = await fixture.EvidenceAsync();
        var reader = new ReusingPolicyReader(fixture.Reader);

        // Act
        var failed = history
            ? !(await new ListArtifactLegalHoldsHandler(reader).HandleAsync(new Cntryl.Portia.RequestContext<ListArtifactLegalHolds>(
                new(fixture.Tenant, "evidence_artifact", id), fixture.Http), CancellationToken.None)).IsSuccess
            : !(await new ArtifactRetentionRead(reader, TimeProvider.System).GetAsync(fixture.Tenant,
                "evidence_artifact", id, null, CancellationToken.None)).IsSuccess;

        // Assert
        Assert.True(failed);
    }

    sealed class ReusingPolicyReader(IAggregateReader inner) : IAggregateReader
    {
        ArtifactRetention? _shared;
        public async ValueTask<T> HydrateAsync<T>(T aggregate, CancellationToken ct = default) where T : Aggregate
        {
            if (aggregate is not ArtifactRetention retention)
            {
                if (aggregate is EvidenceArtifact)
                    _sourceId = aggregate.Id;
                return await inner.HydrateAsync(aggregate, ct);
            }
            if (_shared is null)
                _shared = retention;
            else
            {
                var tenant = Uuid.Parse(retention.Stream.Realm, null);
                var source = new ArtifactRetentionSource(tenant, "evidence_artifact", _sourceId, Fixture.Sha);
                var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Org Admin");
                var now = DateTimeOffset.UtcNow;
                new AggregateScenario<ArtifactRetention>(_shared).Given(
                    DomainEventSeed.Attach(new ArtifactRetentionBound(source, 1, 1, actor, now), _shared.Id, 1),
                    DomainEventSeed.Attach(new ArtifactLegalHoldPlaced(source, 2, 1, Uuid.CreateVersion4(), "Concurrent hold", actor, now), _shared.Id, 2));
            }
            return (T)(Aggregate)_shared;
        }
        Uuid _sourceId;
    }

    [Theory]
    [InlineData("basis", "direct", false)]
    [InlineData("basis", "mcp", false)]
    [InlineData("basis", "http", true)]
    [InlineData("place", "direct", false)]
    [InlineData("place", "mcp", false)]
    [InlineData("place", "http", true)]
    [InlineData("release", "direct", false)]
    [InlineData("release", "mcp", false)]
    [InlineData("release", "http", true)]
    public async Task ShouldRequirePersonalHttpInvocationGivenRetentionDecision(string operation, string transport, bool allowed)
    {
        // Arrange
        await using var fixture = new Fixture();
        var id = await fixture.EvidenceAsync();
        var hold = Uuid.CreateVersion4();
        if (operation == "release")
            Assert.True((await new PlaceArtifactLegalHoldHandler(fixture.Mutation).HandleAsync(
                new Cntryl.Portia.RequestContext<PlaceArtifactLegalHold>(new(fixture.Tenant, "evidence_artifact", id,
                    Fixture.Sha, hold, 0, "Preserve"), new RequestDispatchContext(fixture.Actor,
                    new HttpInvocation("POST", "/api/v1/tenants/retention/legal-holds", null, "retention-hold-test"))), CancellationToken.None)).IsSuccess);
        var dispatch = transport switch
        {
            "mcp" => new RequestDispatchContext(fixture.Actor, new McpInvocation("retention-decision")),
            "http" => new RequestDispatchContext(fixture.Actor, new HttpInvocation("POST", "/api/v1/tenants/retention/decisions", null, "retention-decision-test")),
            _ => new RequestDispatchContext(fixture.Actor),
        };

        // Act
        var result = operation switch
        {
            "basis" => await new RecordArtifactRetentionBasisHandler(fixture.Mutation).HandleAsync(
                new Cntryl.Portia.RequestContext<RecordArtifactRetentionBasis>(new(fixture.Tenant, "evidence_artifact", id,
                    Fixture.Sha, 0, "Verified"), dispatch), CancellationToken.None),
            "place" => await new PlaceArtifactLegalHoldHandler(fixture.Mutation).HandleAsync(
                new Cntryl.Portia.RequestContext<PlaceArtifactLegalHold>(new(fixture.Tenant, "evidence_artifact", id,
                    Fixture.Sha, hold, 0, "Preserve"), dispatch), CancellationToken.None),
            _ => await new ReleaseArtifactLegalHoldHandler(fixture.Mutation).HandleAsync(
                new Cntryl.Portia.RequestContext<ReleaseArtifactLegalHold>(new(fixture.Tenant, "evidence_artifact", id,
                    Fixture.Sha, hold, 2, "Release"), dispatch), CancellationToken.None),
        };
        var retained = await fixture.Reader.HydrateAsync(new ArtifactRetention(fixture.Tenant, "evidence_artifact", id));

        // Assert
        Assert.Equal(allowed, result.IsSuccess);
        Assert.Equal(allowed ? operation == "release" ? 3 : 2 : operation == "release" ? 2 : 0, retained.Revision);
    }

    [Fact]
    public async Task ShouldKeepReadsMcpSafeGivenAdminRetentionConsumer()
    {
        // Arrange
        await using var fixture = new Fixture();
        var id = await fixture.EvidenceAsync();
        var batch = await fixture.ImportAsync();
        var dispatch = new RequestDispatchContext(fixture.Actor, new McpInvocation("retention-read"));

        // Act
        var artifact = await new GetArtifactRetentionHandler(fixture.Read).HandleAsync(
            new Cntryl.Portia.RequestContext<GetArtifactRetention>(new(fixture.Tenant, "evidence_artifact", id), dispatch), CancellationToken.None);
        var holds = await new ListArtifactLegalHoldsHandler(fixture.Reader).HandleAsync(
            new Cntryl.Portia.RequestContext<ListArtifactLegalHolds>(new(fixture.Tenant, "evidence_artifact", id), dispatch), CancellationToken.None);
        var import = await new GetApplicationImportRetentionHandler(fixture.Read).HandleAsync(
            new Cntryl.Portia.RequestContext<GetApplicationImportRetention>(new(fixture.Tenant, batch), dispatch), CancellationToken.None);

        // Assert
        Assert.True(artifact.IsSuccess);
        Assert.True(holds.IsSuccess);
        Assert.True(import.IsSuccess);
    }

    static ClaimsPrincipal BdgrzActor(Uuid user) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", user.ToString())], "BdgrzSession"));

    sealed class ActiveTenant : ITenantActivity
    {
        public ValueTask<bool> IsActiveAsync(Uuid tenantId, CancellationToken ct = default) => ValueTask.FromResult(true);
    }

    sealed class Fixture : IAsyncDisposable
    {
        public const string Sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public ClaimsPrincipal Actor { get; } = BdgrzActor(Uuid.CreateVersion4());
        public RequestDispatchContext Http => new(Actor,
            new HttpInvocation("POST", "/api/v1/tenants/retention", null, "retention-test"));
        public IAggregateReader Reader { get; }
        public IAggregateExecutor Executor { get; }
        public IAggregateWriter Writer { get; }
        public ArtifactRetentionMutation Mutation { get; }
        public ArtifactRetentionRead Read { get; }

        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            services.AddSingleton(TimeProvider.System);
            services.AddPortia();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
            Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            Mutation = new(Executor, Reader, TimeProvider.System);
            Read = new(Reader, TimeProvider.System);
        }

        public async Task<Uuid> EvidenceAsync()
        {
            var id = Uuid.CreateVersion4();
            var artifact = new EvidenceArtifact(Tenant, id);
            Assert.True(artifact.Register(new("Payroll access", null, "export", "manual", DateTimeOffset.UtcNow,
                new(2019, 1, 1), new(2019, 12, 31), "confidential"), Sha, 100,
                ActorReference.ForMember(Uuid.CreateVersion4(), "Collector"), DateTimeOffset.UtcNow).IsSuccess);
            await Writer.SaveAsync(artifact, new Cntryl.Portia.RequestContext<GetArtifactRetention>(new(Tenant, "evidence_artifact", id), Actor));
            return id;
        }

        public async Task<Uuid> ImportAsync()
        {
            var staged = await new StageApplicationImportHandler(Executor, Reader, TimeProvider.System).HandleAsync(
                new Cntryl.Portia.RequestContext<StageApplicationImport>(new(Tenant, Uuid.CreateVersion4(), "manual", "applications", "partial",
                    [new("row-1", "Payroll", "Pay staff", null)]), Actor), CancellationToken.None);
            Assert.True(staged.IsSuccess);
            return staged.Value.BatchId;
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }
}

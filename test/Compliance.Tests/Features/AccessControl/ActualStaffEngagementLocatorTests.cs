using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class ActualStaffEngagementLocatorTests
{
    [Fact]
    public async Task ShouldDiscoverActualStaffGivenRetainedAcceptance()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var source = Assert.Single(await fixture.AcceptanceRecordsAsync());

        // Act
        await fixture.ProjectAsync();
        var directory = fixture.Services.GetRequiredService<IActualStaffEngagementLocatorReader>();
        var page = await directory.ListAsync(fixture.Tenant, 200, null);

        // Assert
        var row = Assert.Single(page.Items);
        Assert.Equal(fixture.Tenant, row.TenantId);
        Assert.Equal(fixture.Staff.StaffMemberId, row.StaffMemberId);
        Assert.Equal(fixture.Staff.UserId, row.UserId);
        var accepted = Assert.IsType<ServiceEngagementAcceptanceRecorded>(source.Event);
        Assert.Equal(accepted.ExpectedSequence + 1, row.AcceptanceSourceSequence);
        var ledger = await fixture.HydrateLedgerAsync();
        Assert.Equal(ledger.Sequence, row.AcceptanceSourceSequence);
        Assert.Equal(source.ResourceOffset, row.SourceResourceOffset);
        Assert.Equal(source.NextCursor.Value, row.SourceNextCursor);
        Assert.Equal(source.Stream.Realm, row.SourceRealm);
        Assert.Equal(source.Stream.Area, row.SourceArea);
        Assert.Equal(source.Stream.Resource, row.SourceResource);
        Assert.Equal(source.NextCursor, (await directory.LoadCheckpointAsync(fixture.Tenant)).Cursor);
        Assert.Null(page.NextCursor);
        Assert.Empty((await directory.ListAsync(Uuid.CreateVersion4(), 200, null)).Items);
    }

    [Fact]
    public async Task ShouldPageEveryPermanentLocatorGivenMoreThanTwoHundredRetainedAssignments()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(staffCount: 30, engagementCount: 7);
        await fixture.ProjectAsync();
        var directory = fixture.Services.GetRequiredService<IActualStaffEngagementLocatorReader>();
        var rows = new List<ActualStaffEngagementLocatorView>();
        string? cursor = null;

        // Act
        do
        {
            var page = await directory.ListAsync(fixture.Tenant, 200, cursor);
            rows.AddRange(page.Items);
            cursor = page.NextCursor;
        } while (cursor is not null);
        await fixture.ProjectAsync();

        // Assert
        Assert.Equal(210, rows.Count);
        Assert.Equal(210, rows.Select(row => row.LocatorId).Distinct().Count());
        Assert.Equal(7, (await directory.ListForStaffAsync(fixture.Tenant, fixture.Staff.StaffMemberId,
            fixture.Staff.UserId, 200, null)).Items.Count);
        Assert.Empty((await directory.ListForStaffAsync(fixture.Tenant, fixture.Staff.StaffMemberId,
            Uuid.CreateVersion4(), 200, null)).Items);
        Assert.Empty((await directory.ListForStaffAsync(Uuid.CreateVersion4(), fixture.Staff.StaffMemberId,
            fixture.Staff.UserId, 200, null)).Items);
    }

    [Theory]
    [InlineData("accepted_snapshot")]
    [InlineData("source_intent")]
    public async Task ShouldKeepOriginalLocatorAndCheckpointGivenConflictingImmutableSourceReplay(string mutation)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await fixture.ProjectAsync();
        var directory = fixture.Services.GetRequiredService<IActualStaffEngagementLocatorReader>();
        var before = await directory.LoadCheckpointAsync(fixture.Tenant);
        var original = Assert.Single((await directory.ListAsync(fixture.Tenant, 200, null)).Items);
        var source = Assert.Single(await fixture.AcceptanceRecordsAsync());
        var accepted = Assert.IsType<ServiceEngagementAcceptanceRecorded>(source.Event);
        var altered = source with
        {
            Event = mutation == "accepted_snapshot"
                ? accepted with { Acceptance = accepted.Acceptance with { AuthorityReference = "Changed retained evidence" } }
                : accepted with { Intent = "Changed original acceptance intent" },
        };
        var projection = fixture.Services.GetRequiredService<IActualStaffEngagementLocatorProjection>();

        // Act
        await using (var batch = await projection.BeginAsync(new ProjectionBatchContext(fixture.Identity, before)))
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await projection.ApplyAsync(altered, new Context(fixture.Identity), CancellationToken.None));

        // Assert
        Assert.Equal(before, await directory.LoadCheckpointAsync(fixture.Tenant));
        Assert.Equal(original, Assert.Single((await directory.ListAsync(fixture.Tenant, 200, null)).Items));
    }

    [Fact]
    public async Task ShouldPreserveExactLocatorGivenIdenticalSourceRedelivery()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await fixture.ProjectAsync();
        var directory = fixture.Services.GetRequiredService<IActualStaffEngagementLocatorReader>();
        var checkpoint = await directory.LoadCheckpointAsync(fixture.Tenant);
        var original = Assert.Single((await directory.ListAsync(fixture.Tenant, 200, null)).Items);
        var source = Assert.Single(await fixture.AcceptanceRecordsAsync());
        var projection = fixture.Services.GetRequiredService<IActualStaffEngagementLocatorProjection>();

        // Act
        await using (var batch = await projection.BeginAsync(new ProjectionBatchContext(fixture.Identity, checkpoint)))
        {
            await projection.ApplyAsync(source, new Context(fixture.Identity), CancellationToken.None);
            await batch.CommitAsync(checkpoint);
        }

        // Assert
        Assert.Equal(checkpoint, await directory.LoadCheckpointAsync(fixture.Tenant));
        Assert.Equal(original, Assert.Single((await directory.ListAsync(fixture.Tenant, 200, null)).Items));
    }

    [Fact]
    public async Task ShouldRetainPermanentLocatorsGivenRemovalAndClosure()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(staffCount: 2);
        await fixture.ProjectAsync();
        var directory = fixture.Services.GetRequiredService<IActualStaffEngagementLocatorReader>();
        var before = await directory.ListAsync(fixture.Tenant, 200, null);
        var checkpoint = await directory.LoadCheckpointAsync(fixture.Tenant);

        // Act
        await fixture.RemoveThenCloseAsync();
        await fixture.ProjectAsync();

        // Assert
        Assert.Equal(before.Items.OrderBy(row => row.LocatorId.ToString()),
            (await directory.ListAsync(fixture.Tenant, 200, null)).Items.OrderBy(row => row.LocatorId.ToString()));
        Assert.NotEqual(checkpoint, await directory.LoadCheckpointAsync(fixture.Tenant));
        Assert.Equal(2, before.Items.Count);
    }

    [Fact]
    public async Task ShouldRollbackLocatorAndCheckpointGivenCommitFailureThenRetry()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(staffCount: 2);
        var records = await fixture.SourceRecordsAsync();
        var prior = records[^2].NextCursor;
        fixture.Faults.FailNextLocatorCommit = true;
        var directory = fixture.Services.GetRequiredService<IActualStaffEngagementLocatorReader>();

        // Act
        await Assert.ThrowsAsync<IOException>(() => fixture.ProjectAsync());

        // Assert
        Assert.Equal(prior, (await directory.LoadCheckpointAsync(fixture.Tenant)).Cursor);
        Assert.Empty((await directory.ListAsync(fixture.Tenant, 200, null)).Items);
        await fixture.ProjectAsync();
        Assert.Equal(2, (await directory.ListAsync(fixture.Tenant, 200, null)).Items.Count);
        Assert.Equal(records[^1].NextCursor, (await directory.LoadCheckpointAsync(fixture.Tenant)).Cursor);
    }

    [Fact]
    public async Task ShouldSeparateRetainedLocatorsGivenSameCanonicalStaffInTwoClients()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var otherTenant = Uuid.CreateVersion4();
        await fixture.SeedOtherTenantAsync(otherTenant);
        await fixture.ProjectAsync();
        await fixture.ProjectOtherTenantAsync(otherTenant);
        var directory = fixture.Services.GetRequiredService<IActualStaffEngagementLocatorReader>();

        // Act
        var first = await directory.ListForStaffAsync(fixture.Tenant, fixture.Staff.StaffMemberId, fixture.Staff.UserId, 200, null);
        var second = await directory.ListForStaffAsync(otherTenant, fixture.Staff.StaffMemberId, fixture.Staff.UserId, 200, null);

        // Assert
        Assert.Equal(fixture.Tenant, Assert.Single(first.Items).TenantId);
        Assert.Equal(otherTenant, Assert.Single(second.Items).TenantId);
        Assert.NotEqual(first.Items[0].LocatorId, second.Items[0].LocatorId);
        Assert.NotEqual(first.Items[0].EngagementId, second.Items[0].EngagementId);
        Assert.NotEqual(first.Items[0].SourceRealm, second.Items[0].SourceRealm);
    }

    [Fact]
    public async Task ShouldRejectForeignContextBeforeRowsGivenOpenOwningTenantBatch()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var source = Assert.Single(await fixture.AcceptanceRecordsAsync());
        var accepted = Assert.IsType<ServiceEngagementAcceptanceRecorded>(source.Event);
        var otherTenant = Uuid.CreateVersion4();
        var altered = source with
        {
            Stream = new EventStreamAddress(otherTenant.ToString(), "client-independence", otherTenant.ToString()),
            Event = accepted with { TenantId = otherTenant, Acceptance = accepted.Acceptance with { TenantId = otherTenant } },
        };
        var projection = fixture.Services.GetRequiredService<IActualStaffEngagementLocatorProjection>();
        var directory = fixture.Services.GetRequiredService<IActualStaffEngagementLocatorReader>();
        var foreignContext = new Context(new CheckpointIdentity("ActualStaffEngagementLocatorV1",
            EventStreamPattern.ForPattern(otherTenant.ToString(), "client-independence")));

        // Act
        await using (var batch = await projection.BeginAsync(new ProjectionBatchContext(fixture.Identity, ProjectionCheckpoint.Start)))
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await projection.ApplyAsync(altered, foreignContext, CancellationToken.None));

        // Assert
        Assert.Empty((await directory.ListAsync(fixture.Tenant, 200, null)).Items);
        Assert.Empty((await directory.ListAsync(otherTenant, 200, null)).Items);
        Assert.Equal(ProjectionCheckpoint.Start, await directory.LoadCheckpointAsync(fixture.Tenant));
    }

    [Theory]
    [InlineData("foreign_stream")]
    [InlineData("wrong_resource")]
    [InlineData("foreign_tenant")]
    [InlineData("foreign_snapshot")]
    [InlineData("empty_user")]
    [InlineData("invalid_practice")]
    [InlineData("invalid_revision")]
    [InlineData("duplicate_staff")]
    [InlineData("future_assignment")]
    [InlineData("overflow_sequence")]
    public async Task ShouldRejectSourceBeforeRowsOrCheckpointGivenMalformedLocatorProvenance(string mutation)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var source = Assert.Single(await fixture.AcceptanceRecordsAsync());
        var accepted = Assert.IsType<ServiceEngagementAcceptanceRecorded>(source.Event);
        var assignment = Assert.Single(accepted.Acceptance.Assignments);
        var altered = mutation switch
        {
            "foreign_stream" => source with { Stream = new EventStreamAddress(Uuid.CreateVersion4().ToString(), "client-independence", fixture.Tenant.ToString()) },
            "wrong_resource" => source with { Stream = new EventStreamAddress(fixture.Tenant.ToString(), "client-independence", Uuid.CreateVersion4().ToString()) },
            "foreign_tenant" => source with { Event = accepted with { TenantId = Uuid.CreateVersion4() } },
            "foreign_snapshot" => source with { Event = accepted with { Acceptance = accepted.Acceptance with { TenantId = Uuid.CreateVersion4() } } },
            "empty_user" => source with { Event = accepted with { Acceptance = accepted.Acceptance with { Assignments = [assignment with { UserId = Uuid.Empty }] } } },
            "invalid_practice" => source with { Event = accepted with { Acceptance = accepted.Acceptance with { Assignments = [assignment with { Practice = "unknown" }] } } },
            "invalid_revision" => source with { Event = accepted with { Acceptance = accepted.Acceptance with { Assignments = [assignment with { DirectoryStaffRevision = 0 }] } } },
            "duplicate_staff" => source with { Event = accepted with { Acceptance = accepted.Acceptance with { Assignments = [assignment, assignment] } } },
            "future_assignment" => source with { Event = accepted with { Acceptance = accepted.Acceptance with { Assignments = [assignment with { AssignedAt = accepted.Acceptance.RecordedAt.AddMinutes(1) }] } } },
            "overflow_sequence" => source with { Event = accepted with { ExpectedSequence = long.MaxValue } },
            _ => throw new InvalidOperationException(),
        };
        var projection = fixture.Services.GetRequiredService<IActualStaffEngagementLocatorProjection>();
        var directory = fixture.Services.GetRequiredService<IActualStaffEngagementLocatorReader>();

        // Act
        await using (var batch = await projection.BeginAsync(new ProjectionBatchContext(fixture.Identity, ProjectionCheckpoint.Start)))
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await projection.ApplyAsync(altered, new Context(fixture.Identity), CancellationToken.None));

        // Assert
        Assert.Equal(ProjectionCheckpoint.Start, await directory.LoadCheckpointAsync(fixture.Tenant));
        Assert.Empty((await directory.ListAsync(fixture.Tenant, 200, null)).Items);
    }

    sealed record Context(CheckpointIdentity Identity) : IProjectorContext
    {
        public bool IsRebuild => false;
    }

    internal sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        ActorReference _clientActor = null!;
        bool _bound;
        static readonly DateTimeOffset Now = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public FirmStaffMemberView Staff { get; private set; } = null!;
        public IServiceProvider Services => _scope.ServiceProvider;
        internal IServiceProvider Provider => _provider;
        public Faults Faults { get; } = new();
        public CheckpointIdentity Identity => new("ActualStaffEngagementLocatorV1",
            EventStreamPattern.ForPattern(Tenant.ToString(), "client-independence"));

        Fixture(TimeProvider? clock = null)
        {
            var services = new ServiceCollection();
            services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance",
            }).Build(), developerAuthentication: true);
            if (clock is not null)
                services.AddSingleton(clock);
            var events = new InMemoryEventStore();
            services.AddSingleton<IEventStore>(new FaultingEventStore(events, Faults));
            services.AddSingleton<IDomainEventReader>(events);
            services.AddSingleton<IKvClient>(new FaultingKvClient(new InMemoryKvClient(), Faults));
            services.AddScoped<IActualStaffEngagementLocatorReader>(provider =>
                new FaultingLocatorReader(provider.GetRequiredService<FitzActualStaffEngagementLocator>(), Faults));
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
        }

        public static async Task<Fixture> CreateAsync(int staffCount = 1, int engagementCount = 1, TimeProvider? clock = null, int serviceCount = 0)
        {
            var fixture = new Fixture(clock);
            var actor = ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Synthetic operator");
            FirmStaffMemberView? partner = null;
            var staff = new List<FirmStaffMemberView>();
            await ProgramManagementServices.SeedAsync(fixture._provider, new FirmStaffDirectory(), directory =>
            {
                for (var i = 0; i < staffCount; i++)
                {
                    var registered = directory.Register(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(),
                        "attest", "Explicit synthetic assignee source", directory.Sequence, actor, Now);
                    Assert.True(registered.IsSuccess);
                    staff.Add(registered.Value!);
                }
                fixture.Staff = staff[0];
                var registeredPartner = directory.Register(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(),
                    "attest", "Explicit synthetic partner identity source", directory.Sequence, actor, Now);
                Assert.True(registeredPartner.IsSuccess);
                partner = registeredPartner.Value;
                return Result.Success;
            });
            var administrator = Uuid.CreateVersion4();
            var client = ActorReference.ForMember(RbacIds.Member(fixture.Tenant, administrator), "Synthetic client administrator");
            fixture._clientActor = client;
            for (var index = 0; index < engagementCount; index++)
            {
                var engagement = Uuid.CreateVersion4();
                var acknowledgement = Uuid.CreateVersion4();
                await ProgramManagementServices.SeedAsync(fixture._provider, new IndependenceLedger(fixture.Tenant), ledger =>
                {
                    if (index == 0)
                        for (var serviceIndex = 0; serviceIndex < serviceCount; serviceIndex++)
                            Assert.True(ledger.RecordService(Uuid.CreateVersion4(), Uuid.CreateVersion4(), ledger.Sequence,
                                new NonattestServiceContent(Uuid.CreateVersion4(), "readiness", new DateOnly(2010, 1, 1),
                                    new DateOnly(2010, 12, 31), [fixture.Staff.StaffMemberId], false,
                                    "Synthetic historical service source " + new string('s', 1900)), client, Now).IsSuccess);
                    Assert.True(ledger.CreateEngagement(Uuid.CreateVersion4(), engagement, ledger.Sequence,
                        new ServiceEngagementDraftContent("attest", "Synthetic scope", new DateOnly(2026, 1, 1),
                            new DateOnly(2026, 12, 31), fixture.Staff.StaffMemberId), fixture.Staff, client, Now).IsSuccess);
                    foreach (var additional in staff.Skip(1))
                        Assert.True(ledger.ProposeEngagementStaff(Uuid.CreateVersion4(), engagement, ledger.Sequence, additional, client, Now).IsSuccess);
                    var revision = ledger.Engagement(engagement)!.Revision;
                    Assert.True(ledger.AcknowledgeManagement(Uuid.CreateVersion4(), new AcknowledgeEngagementManagement(
                        fixture.Tenant, engagement, acknowledgement, ledger.Sequence, revision,
                        ledger.History().Services.Select(service => service.ServiceRecordId).ToArray(), "Management retains responsibility"),
                        administrator, client, Now).IsSuccess);
                    // Synthetic internal proof and ratification exercise the engine; no public authority is inferred.
                    var proof = new VerifiedEngagementAcceptance(fixture.Tenant, engagement, revision, Uuid.CreateVersion4(),
                        partner!.StaffMemberId, partner.UserId, "Synthetic verified authority ONLY", null,
                        acknowledgement, null, null, staff, partner, 1, Now);
                    var rules = new IndependenceRuleVersionView(1, new IndependenceRuleContent(12,
                        [new IndependenceServiceRuleContent("readiness", "conditionally_compatible", "impairing")],
                        "Synthetic ratified test rules ONLY"), actor, Now, true);
                    return ledger.AcceptEngagement(Uuid.CreateVersion4(), ledger.Sequence, proof, rules, Now);
                });
            }
            return fixture;
        }

        public async Task ProjectAsync()
        {
            var registration = Assert.Single(Services.GetServices<WorkloadRegistration>(),
                item => item.Name == "ActualStaffEngagementLocatorV1");
            Assert.Equal(WorkloadScope.PerTenant, registration.Scope);
            var projector = (Projector)Services.GetRequiredService(registration.ComponentType);
            if (!_bound)
            {
                // Public Portia testing seam binds the registered component; actual retained source drives the real pass below.
                await new ProjectorScenario(new TenantId(Tenant.ToString())).RunAsync(projector);
                _bound = true;
            }
            var reader = Services.GetRequiredService<IActualStaffEngagementLocatorReader>();
            var runner = new ProjectorRunner(Services.GetRequiredService<IDomainEventReader>());
            var checkpoint = await reader.LoadCheckpointAsync(Tenant);
            while (true)
            {
                var next = await runner.RunAsync(projector, checkpoint);
                if (next == checkpoint)
                    return;
                checkpoint = next;
            }
        }

        public async Task<List<DomainEventRecord>> AcceptanceRecordsAsync()
            => (await SourceRecordsAsync()).Where(record => record.Event is ServiceEngagementAcceptanceRecorded).ToList();

        public Task<IndependenceLedger> HydrateLedgerAsync() =>
            ProgramManagementServices.HydrateAsync(_provider, new IndependenceLedger(Tenant));

        public async Task<List<DomainEventRecord>> SourceRecordsAsync()
        {
            var records = new List<DomainEventRecord>();
            await foreach (var record in Services.GetRequiredService<IDomainEventReader>().ReadAsync(
                               new IndependenceLedger(Tenant).Stream, 0))
                records.Add(record);
            return records;
        }

        public async Task RemoveThenCloseAsync()
        {
            await ProgramManagementServices.SeedAsync(_provider, new IndependenceLedger(Tenant), ledger =>
            {
                var engagement = Assert.Single(ledger.Engagements);
                var acceptance = ledger.Acceptance(engagement.EngagementId)!;
                var other = Assert.Single(acceptance.Assignments, staff => staff.StaffMemberId != Staff.StaffMemberId);
                Assert.True(ledger.RemoveActualStaff(Uuid.CreateVersion4(), engagement.EngagementId, other.StaffMemberId,
                    ledger.Sequence, "Synthetic internal assignment removal", _clientActor, Now.AddMinutes(1)).IsSuccess);
                return ledger.CloseEngagement(Uuid.CreateVersion4(), engagement.EngagementId, ledger.Sequence,
                    "Synthetic client closure", _clientActor, Now.AddMinutes(2));
            });
        }

        public async Task SeedOtherTenantAsync(Uuid otherTenant, FirmStaffMemberView? assignedStaff = null, DateTimeOffset? recordedAt = null)
        {
            var assignee = assignedStaff ?? Staff;
            var at = recordedAt ?? Now;
            var directory = await ProgramManagementServices.HydrateAsync(_provider, new FirmStaffDirectory());
            var partner = Assert.Single(directory.View().Staff, item => item.StaffMemberId != assignee.StaffMemberId);
            var administrator = Uuid.CreateVersion4();
            var client = ActorReference.ForMember(RbacIds.Member(otherTenant, administrator), "Synthetic second client administrator");
            var engagement = Uuid.CreateVersion4();
            var acknowledgement = Uuid.CreateVersion4();
            await ProgramManagementServices.SeedAsync(_provider, new IndependenceLedger(otherTenant), ledger =>
            {
                Assert.True(ledger.CreateEngagement(Uuid.CreateVersion4(), engagement, ledger.Sequence,
                    new ServiceEngagementDraftContent("attest", "Synthetic second client scope", new DateOnly(2026, 1, 1),
                        new DateOnly(2026, 12, 31), assignee.StaffMemberId), assignee, client, at).IsSuccess);
                Assert.True(ledger.AcknowledgeManagement(Uuid.CreateVersion4(), new AcknowledgeEngagementManagement(
                    otherTenant, engagement, acknowledgement, ledger.Sequence,
                        ledger.Engagement(engagement)!.Revision, ledger.History().Services.Select(service => service.ServiceRecordId).ToArray(), "Management retains responsibility"),
                    administrator, client, at).IsSuccess);
                var proof = new VerifiedEngagementAcceptance(otherTenant, engagement, 1, Uuid.CreateVersion4(),
                    partner.StaffMemberId, partner.UserId, "Synthetic verified authority ONLY", null,
                    acknowledgement, null, null, [assignee], partner, 1, at);
                var rules = new IndependenceRuleVersionView(1, new IndependenceRuleContent(12,
                    [new IndependenceServiceRuleContent("readiness", "conditionally_compatible", "impairing")],
                    "Synthetic ratified test rules ONLY"), partner.Actor, at, true);
                return ledger.AcceptEngagement(Uuid.CreateVersion4(), ledger.Sequence, proof, rules, at);
            });
        }

        public async Task ProjectOtherTenantAsync(Uuid otherTenant)
        {
            await using var scope = _provider.CreateAsyncScope();
            var registration = Assert.Single(scope.ServiceProvider.GetServices<WorkloadRegistration>(),
                item => item.Name == "ActualStaffEngagementLocatorV1");
            var projector = (Projector)scope.ServiceProvider.GetRequiredService(registration.ComponentType);
            await new ProjectorScenario(new TenantId(otherTenant.ToString())).RunAsync(projector);
            var runner = new ProjectorRunner(scope.ServiceProvider.GetRequiredService<IDomainEventReader>());
            var reader = scope.ServiceProvider.GetRequiredService<IActualStaffEngagementLocatorReader>();
            var checkpoint = await reader.LoadCheckpointAsync(otherTenant);
            while (true)
            {
                var next = await runner.RunAsync(projector, checkpoint);
                if (next == checkpoint)
                    return;
                checkpoint = next;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    internal sealed class Faults
    {
        public bool FailNextLocatorCommit { get; set; }
        public Func<Task>? OnNextLocatorScan { get; set; }
        public bool FailNextDirectoryReceiptWrite { get; set; }
        public Func<Task>? OnNextDirectoryReceiptWrite { get; set; }
        public string? LocatorPageFault { get; set; }
    }

    sealed class FaultingLocatorReader(IActualStaffEngagementLocatorReader inner, Faults faults) : IActualStaffEngagementLocatorReader
    {
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default) =>
            inner.LoadCheckpointAsync(tenantId, ct);
        public ValueTask<Page<ActualStaffEngagementLocatorView>> ListAsync(Uuid tenantId, int limit, string? cursor, CancellationToken ct = default) =>
            inner.ListAsync(tenantId, limit, cursor, ct);
        public async ValueTask<Page<ActualStaffEngagementLocatorView>> ListForStaffAsync(Uuid tenantId, Uuid staffMemberId,
            Uuid userId, int limit, string? cursor, CancellationToken ct = default)
        {
            var page = await inner.ListForStaffAsync(tenantId, staffMemberId, userId, limit, cursor, ct);
            return faults.LocatorPageFault switch
            {
                "missing" => new Page<ActualStaffEngagementLocatorView>([], page.NextCursor),
                "duplicate" => new Page<ActualStaffEngagementLocatorView>(page.Items.Concat(page.Items).ToArray(), page.NextCursor),
                _ => page
            };
        }
    }

    sealed class FaultingEventStore(IEventStore inner, Faults faults) : IEventStore
    {
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream, ulong offset, CancellationToken ct) =>
            inner.ReadAsync(stream, offset, ct);
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor, CancellationToken ct) =>
            inner.ReadAsync(pattern, cursor, ct);
        public async ValueTask AppendAsync(EventStreamAddress stream, ulong position, IReadOnlyList<DomainEvent> events, CancellationToken ct)
        {
            if (events.OfType<DirectoryIndependenceReevaluated>().Any())
            {
                if (faults.FailNextDirectoryReceiptWrite)
                {
                    faults.FailNextDirectoryReceiptWrite = false;
                    throw new IOException("Synthetic directory receipt writer failure before append.");
                }
                if (faults.OnNextDirectoryReceiptWrite is { } publish)
                {
                    faults.OnNextDirectoryReceiptWrite = null;
                    await publish();
                }
            }
            await inner.AppendAsync(stream, position, events, ct);
        }
    }

    sealed class FaultingKvClient(IKvClient inner, Faults faults) : IKvClient
    {
        public async Task<IKvTransaction> BeginAsync(string route, KvDurability durability,
            KvMode mode = KvMode.ReadWrite, CancellationToken ct = default) =>
            new FaultingTransaction(await inner.BeginAsync(route, durability, mode, ct), faults);

        public Task<KvSubscription> SubscribeAsync(string pattern, CancellationToken ct = default) =>
            inner.SubscribeAsync(pattern, ct);

        sealed class FaultingTransaction(IKvTransaction inner, Faults faults) : IKvTransaction
        {
            bool _inserted;
            public string Route => inner.Route;
            public Task<KvGetResult> GetAsync(ReadOnlyMemory<byte> key, CancellationToken ct = default) => inner.GetAsync(key, ct);
            public Task PutAsync(ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> value, CancellationToken ct = default) => inner.PutAsync(key, value, ct);
            public Task InsertAsync(ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> value, CancellationToken ct = default)
            {
                _inserted = true;
                return inner.InsertAsync(key, value, ct);
            }
            public Task DeleteAsync(ReadOnlyMemory<byte> key, CancellationToken ct = default) => inner.DeleteAsync(key, ct);
            public Task DeleteRangeAsync(ReadOnlyMemory<byte> startKey, ReadOnlyMemory<byte> endKey,
                CancellationToken ct = default) => inner.DeleteRangeAsync(startKey, endKey, ct);
            public async Task<KvScanResult> ScanAsync(KvScanQuery query, CancellationToken ct = default)
            {
                var result = await inner.ScanAsync(query, ct);
                if (Route.Contains("actual-staff-engagement-locator", StringComparison.Ordinal) && faults.OnNextLocatorScan is { } publish)
                {
                    faults.OnNextLocatorScan = null;
                    await publish();
                }
                return result;
            }
            public Task CommitAsync(CancellationToken ct = default)
            {
                if (_inserted && faults.FailNextLocatorCommit && Route.Contains("actual-staff-engagement-locator", StringComparison.Ordinal))
                {
                    faults.FailNextLocatorCommit = false;
                    throw new IOException("Synthetic locator commit failure before durable commit.");
                }
                return inner.CommitAsync(ct);
            }
            public Task RollbackAsync(CancellationToken ct = default) => inner.RollbackAsync(ct);
            public ValueTask DisposeAsync() => inner.DisposeAsync();
        }
    }
}

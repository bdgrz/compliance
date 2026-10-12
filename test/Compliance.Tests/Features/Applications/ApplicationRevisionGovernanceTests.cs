using System.Security.Claims;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationRevisionGovernanceTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldRejectIncompleteResultGivenGovernedRevision()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        var before = await fixture.EventsAsync();

        // Act
        var result = await fixture.Bus.SendAsync(fixture.Request(), fixture.Actor);
        var after = await fixture.EventsAsync();

        // Assert
        Assert.Equal(before, after);
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldCompleteAndRetainNormalizedContentGivenGovernedLegacyRevision()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        await fixture.RecordPersonAsync(fixture.Owner);
        var before = await fixture.EventsAsync();

        // Act
        var first = await fixture.Bus.SendAsync(fixture.CompleteRequest(), fixture.Actor);
        var completed = await fixture.EventsAsync();
        var second = await fixture.Bus.SendAsync(fixture.Request() with { ExpectedRevision = 2 }, fixture.Actor);
        var after = await fixture.EventsAsync();

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(second.IsSuccess, second.Error?.Message);
        Assert.Equal(2, completed.Length);
        Assert.Equal(3, after.Length);
        Assert.Equal(before[0], after[0]);
        var revised = Assert.IsType<ApplicationRevised>(completed[1].Event);
        Assert.Equal(fixture.Tenant, revised.TenantId);
        Assert.Equal(fixture.Application, revised.ApplicationId);
        Assert.Equal(2, revised.Revision);
        Assert.Equal("Revised", revised.Name);
        Assert.Equal("Revised purpose", revised.Purpose);
        Assert.Equal("Attribution", revised.OwnerReference);
        Assert.Equal("internal", revised.Classification);
        Assert.Equal("service", revised.ApplicationKind);
        Assert.Equal("critical", revised.Criticality);
        Assert.Equal(fixture.Owner, revised.SystemOwnerPersonId);
        Assert.Equal(fixture.Owner, revised.AccessOwnerPersonId);
        Assert.Equal(Now.AddMinutes(1), revised.ChangedAt);
        Assert.Equal(ActorReference.ForMember(revised.ActorMemberId, revised.ActorDisplay), revised.StoredActor);
        var retained = Assert.IsType<ApplicationRevised>(after[2].Event);
        Assert.Equal(3, retained.Revision);
        Assert.Equal(revised.Classification, retained.Classification);
        Assert.Equal(revised.SystemOwnerPersonId, retained.SystemOwnerPersonId);
        Assert.Equal(revised.AccessOwnerPersonId, retained.AccessOwnerPersonId);
        Assert.Equal(revised.ApplicationKind, retained.ApplicationKind);
        Assert.Equal(revised.Criticality, retained.Criticality);
        var directory = new FitzApplicationDirectory(new InMemoryKvClient());
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         new CheckpointIdentity("ApplicationDirectoryV2", EventStreamPattern.ForPattern(
                             fixture.Tenant.ToString())), ProjectionCheckpoint.Start)))
        {
            foreach (var record in after)
                await directory.ApplyAsync(record.Event);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var current = await directory.GetAsync(fixture.Tenant, fixture.Application);
        var original = await directory.GetRevisionAsync(fixture.Tenant, fixture.Application, 1);
        var exact = await directory.GetRevisionAsync(fixture.Tenant, fixture.Application, 2);
        var history = await directory.ListRevisionsAsync(fixture.Tenant, fixture.Application, 10, null);
        Assert.Equal("service", current?.ApplicationKind);
        Assert.Equal("critical", current?.Criticality);
        Assert.Equal("manual", current?.SourceKind);
        Assert.Equal(fixture.Application.ToString(), current?.SourceIdentifier);
        Assert.Equal("active", current?.Lifecycle);
        Assert.Equal("Legacy", original?.Name);
        Assert.Equal("Legacy purpose", original?.Purpose);
        Assert.Equal(Now, original?.LastChangedAt);
        Assert.Equal(ActorReference.ForMember(fixture.User, "Original"), original?.Actor);
        Assert.Null(original?.ApplicationKind);
        Assert.Contains("application_kind_missing", original!.Unresolved);
        Assert.Equal("service", exact?.ApplicationKind);
        Assert.Equal("critical", exact?.Criticality);
        Assert.Equal(revised.Actor, exact?.Actor);
        Assert.Equal(revised.ChangedAt, exact?.LastChangedAt);
        Assert.DoesNotContain("system_owner_missing", exact!.Unresolved);
        Assert.DoesNotContain("access_owner_missing", exact.Unresolved);
        Assert.DoesNotContain("application_kind_missing", exact.Unresolved);
        Assert.DoesNotContain("criticality_missing", exact.Unresolved);
        Assert.Contains("system_instances_missing", exact.Unresolved);
        Assert.Contains("classification_unverified", exact.Unresolved);
        Assert.Equal(3, history?.Items.Count);
        Assert.Equal("Legacy", history?.Items[0].Name);
        Assert.Equal("service", history?.Items[1].ApplicationKind);
    }

    [Theory]
    [InlineData("name_blank")]
    [InlineData("name_missing")]
    [InlineData("purpose_blank")]
    [InlineData("purpose_missing")]
    [InlineData("name_large")]
    [InlineData("purpose_large")]
    [InlineData("owner_reference_large")]
    [InlineData("classification_missing")]
    [InlineData("classification_blank")]
    [InlineData("classification_large")]
    [InlineData("kind_missing")]
    [InlineData("kind_blank")]
    [InlineData("kind_large")]
    [InlineData("criticality_missing")]
    [InlineData("criticality_blank")]
    [InlineData("criticality_large")]
    [InlineData("system_owner_missing")]
    [InlineData("access_owner_missing")]
    [InlineData("system_owner_empty")]
    [InlineData("access_owner_empty")]
    public async Task ShouldRejectInvalidResultWithoutAppendGivenGovernedRevision(string invalid)
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        await fixture.RecordPersonAsync(fixture.Owner);
        var request = fixture.CompleteRequest();
        request = invalid switch
        {
            "name_blank" => request with { Name = " \t " },
            "name_missing" => request with { Name = null! },
            "purpose_blank" => request with { Purpose = " \t " },
            "purpose_missing" => request with { Purpose = null! },
            "name_large" => request with { Name = new string('x', 201) },
            "purpose_large" => request with { Purpose = new string('x', 2001) },
            "owner_reference_large" => request with { OwnerReference = new string('x', 2001) },
            "classification_missing" => request with { Classification = null },
            "classification_blank" => request with { Classification = " \t " },
            "classification_large" => request with { Classification = new string('x', 201) },
            "kind_missing" => request with { ApplicationKind = null },
            "kind_blank" => request with { ApplicationKind = " \t " },
            "kind_large" => request with { ApplicationKind = new string('x', 201) },
            "criticality_missing" => request with { Criticality = null },
            "criticality_blank" => request with { Criticality = " \t " },
            "criticality_large" => request with { Criticality = new string('x', 201) },
            "system_owner_missing" => request with { SystemOwnerPersonId = null },
            "access_owner_missing" => request with { AccessOwnerPersonId = null },
            "system_owner_empty" => request with { SystemOwnerPersonId = Uuid.Empty },
            "access_owner_empty" => request with { AccessOwnerPersonId = Uuid.Empty },
            _ => throw new ArgumentOutOfRangeException(nameof(invalid)),
        };
        var before = await fixture.EventsAsync();
        var foreignBefore = await fixture.ForeignEventsAsync();

        // Act
        var result = await fixture.Bus.SendAsync(request, fixture.Actor);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(before, await fixture.EventsAsync());
        Assert.Equal(foreignBefore, await fixture.ForeignEventsAsync());
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task ShouldRevalidateEffectiveOwnersWithoutAppendGivenMissingOrForeignPerson(
        bool retained, bool foreign, bool accessOwner)
    {
        // Arrange
        await using var fixture = new Fixture();
        var invalidOwner = Uuid.CreateVersion4();
        if (foreign)
            await fixture.RecordPersonAsync(invalidOwner, fixture.ForeignTenant);
        await fixture.RecordPersonAsync(fixture.Owner);
        await fixture.SeedAsync(complete: retained, systemOwner: accessOwner ? fixture.Owner : invalidOwner,
            accessOwner: accessOwner ? invalidOwner : fixture.Owner);
        var request = fixture.CompleteRequest();
        request = accessOwner
            ? request with { AccessOwnerPersonId = retained ? null : invalidOwner }
            : request with { SystemOwnerPersonId = retained ? null : invalidOwner };
        var before = await fixture.EventsAsync();
        var foreignBefore = await fixture.ForeignEventsAsync();

        // Act
        var result = await fixture.Bus.SendAsync(request, fixture.Actor);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(before, await fixture.EventsAsync());
        Assert.Equal(foreignBefore, await fixture.ForeignEventsAsync());
    }

    [Theory]
    [InlineData("missing_grant", RequestErrorKind.Forbidden)]
    [InlineData("nonmember", RequestErrorKind.NotFound)]
    [InlineData("foreign_tenant", RequestErrorKind.NotFound)]
    [InlineData("foreign_resource", RequestErrorKind.NotFound)]
    [InlineData("stale", RequestErrorKind.Conflict)]
    public async Task ShouldPreserveBothTenantStreamsGivenDeniedOrStaleRevision(string denial,
        RequestErrorKind expected)
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        await fixture.RecordPersonAsync(fixture.Owner);
        fixture.Permissions.Allowed = denial != "missing_grant";
        fixture.Memberships.State = denial == "nonmember" ? "nonmember" : "active";
        var request = fixture.CompleteRequest();
        request = denial switch
        {
            "foreign_tenant" => request with { TenantId = fixture.ForeignTenant, ApplicationId = fixture.ForeignApplication },
            "foreign_resource" => request with { ApplicationId = fixture.ForeignApplication },
            "stale" => request with { ExpectedRevision = 0 },
            _ => request,
        };
        var before = await fixture.EventsAsync();
        var foreignBefore = await fixture.ForeignEventsAsync();

        // Act
        var result = await fixture.Bus.SendAsync(request, fixture.Actor);

        // Assert
        Assert.Equal(expected, result.Error?.Kind);
        Assert.Equal(before, await fixture.EventsAsync());
        Assert.Equal(foreignBefore, await fixture.ForeignEventsAsync());
    }

    [Fact]
    public async Task ShouldRejectEarlierOwnerObservationGivenConcurrentRevisionBeforeExecution()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.RecordPersonAsync(fixture.Owner);
        await fixture.SeedAsync(complete: true);
        var before = await fixture.EventsAsync();
        var foreignBefore = await fixture.ForeignEventsAsync();
        var executor = fixture.Transition;
        executor.BeforeApplicationExecution = async () =>
        {
            var concurrent = await fixture.Reader.HydrateAsync(new DeclaredApplication(fixture.Tenant, fixture.Application));
            Assert.Null(concurrent.Revise(1, "Concurrent", "Concurrent purpose", null,
                fixture.User, "Concurrent editor", Now.AddSeconds(30)));
            await fixture.Writer.SaveAsync(concurrent, new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        };

        // Act
        var result = await fixture.Bus.SendAsync(fixture.Request(), fixture.Actor);
        var after = await fixture.EventsAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.True(executor.Transitioned);
        Assert.Equal(2, executor.ValidatedOwnerReads);
        Assert.Equal(2, after.Length);
        Assert.Equal(before[0], after[0]);
        var concurrentEvent = Assert.IsType<ApplicationRevised>(after[1].Event);
        Assert.Equal("Concurrent", concurrentEvent.Name);
        Assert.Equal(2, concurrentEvent.Revision);
        Assert.Null(concurrentEvent.SystemOwnerPersonId);
        Assert.Null(concurrentEvent.AccessOwnerPersonId);
        Assert.Equal(foreignBefore, await fixture.ForeignEventsAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldTrimAdditionsBeforeBoundAndRetainOmissionsGivenEitherRevisionMode(bool enabled)
    {
        // Arrange
        await using var fixture = new Fixture(enabled);
        await fixture.RecordPersonAsync(fixture.Owner);
        await fixture.SeedAsync();
        var value = new string('x', 200);

        // Act
        var added = await fixture.Bus.SendAsync(fixture.CompleteRequest() with
        {
            ApplicationKind = "  " + value + "  ",
            Criticality = "  " + value + "  ",
        }, fixture.Actor);
        var retained = await fixture.Bus.SendAsync(fixture.Request() with { ExpectedRevision = 2 }, fixture.Actor);
        var events = await fixture.EventsAsync();

        // Assert
        Assert.True(added.IsSuccess, added.Error?.Message);
        Assert.True(retained.IsSuccess, retained.Error?.Message);
        var revision = Assert.IsType<ApplicationRevised>(events[2].Event);
        Assert.Equal(value, revision.ApplicationKind);
        Assert.Equal(value, revision.Criticality);
        Assert.Equal(enabled ? "internal" : null, revision.Classification);
        Assert.Equal(enabled ? fixture.Owner : (Uuid?)null, revision.SystemOwnerPersonId);
        Assert.Equal(enabled ? fixture.Owner : (Uuid?)null, revision.AccessOwnerPersonId);
    }

    [Fact]
    public async Task ShouldAllowUnresolvedLegacyEditGivenDisabledRevisionGate()
    {
        // Arrange
        await using var fixture = new Fixture(false);
        await fixture.SeedAsync();

        // Act
        var result = await fixture.Bus.SendAsync(fixture.Request(), fixture.Actor);
        var events = await fixture.EventsAsync();

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var revised = Assert.IsType<ApplicationRevised>(events[1].Event);
        Assert.Null(revised.ApplicationKind);
        Assert.Null(revised.Criticality);
        Assert.Null(revised.Classification);
        Assert.Null(revised.SystemOwnerPersonId);
        Assert.Null(revised.AccessOwnerPersonId);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task ShouldRejectSuppliedInvalidAdditionsGivenEitherRevisionMode(bool enabled,
        bool criticality, bool oversized)
    {
        // Arrange
        await using var fixture = new Fixture(enabled);
        await fixture.SeedAsync();
        await fixture.RecordPersonAsync(fixture.Owner);
        var invalid = oversized ? new string('x', 201) : " \t ";
        var request = criticality
            ? fixture.CompleteRequest() with { Criticality = invalid }
            : fixture.CompleteRequest() with { ApplicationKind = invalid };
        var before = await fixture.EventsAsync();
        var foreignBefore = await fixture.ForeignEventsAsync();

        // Act
        var result = await fixture.Bus.SendAsync(request, fixture.Actor);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(before, await fixture.EventsAsync());
        Assert.Equal(foreignBefore, await fixture.ForeignEventsAsync());
    }

    [Fact]
    public async Task ShouldClearClassificationGivenBlankRevisionWithDisabledGate()
    {
        // Arrange
        await using var fixture = new Fixture(false);
        await fixture.RecordPersonAsync(fixture.Owner);
        await fixture.SeedAsync(complete: true);
        var before = await fixture.EventsAsync();
        var foreignBefore = await fixture.ForeignEventsAsync();

        // Act
        var result = await fixture.Bus.SendAsync(fixture.CompleteRequest() with
        {
            Classification = " \t ",
        }, fixture.Actor);
        var after = await fixture.EventsAsync();

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(2, after.Length);
        Assert.Equal(before[0], after[0]);
        var revised = Assert.IsType<ApplicationRevised>(after[1].Event);
        Assert.Equal(fixture.Tenant, revised.TenantId);
        Assert.Equal(fixture.Application, revised.ApplicationId);
        Assert.Equal(2, revised.Revision);
        Assert.Null(revised.Classification);
        Assert.Equal(fixture.Owner, revised.SystemOwnerPersonId);
        Assert.Equal(fixture.Owner, revised.AccessOwnerPersonId);
        Assert.Equal("service", revised.ApplicationKind);
        Assert.Equal("critical", revised.Criticality);
        Assert.Equal(foreignBefore, await fixture.ForeignEventsAsync());
    }

    [Fact]
    public async Task ShouldRejectExplicitBlankClassificationGivenEnabledRevisionWithCompleteState()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.RecordPersonAsync(fixture.Owner);
        await fixture.SeedAsync(complete: true);
        var before = await fixture.EventsAsync();
        var foreignBefore = await fixture.ForeignEventsAsync();

        // Act
        var result = await fixture.Bus.SendAsync(fixture.CompleteRequest() with
        {
            Classification = " \t ",
        }, fixture.Actor);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(before, await fixture.EventsAsync());
        Assert.Equal(foreignBefore, await fixture.ForeignEventsAsync());
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid Application { get; } = Uuid.CreateVersion4();
        public Uuid User { get; } = Uuid.CreateVersion4();
        public Uuid Owner { get; } = Uuid.CreateVersion4();
        public Uuid ForeignTenant { get; } = Uuid.CreateVersion4();
        public Uuid ForeignApplication { get; } = Uuid.CreateVersion4();
        public ClaimsPrincipal Actor => new(new ClaimsIdentity([
            new Claim("iss", "bdgrz"), new Claim("sub", User.ToString())], "BdgrzSession"));
        public ApplicationImportCompositionTests.Permissions Permissions { get; } = new();
        public ApplicationImportCompositionTests.MembershipDirectory Memberships { get; }
        public IRequestBus Bus => _scope.ServiceProvider.GetRequiredService<IRequestBus>();
        public IAggregateReader Reader => _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        public IAggregateWriter Writer => _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        IEventStore Events => _scope.ServiceProvider.GetRequiredService<IEventStore>();
        public TransitionExecutor Transition => (TransitionExecutor)_scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();

        public Fixture(bool enabled = true)
        {
            var services = new ServiceCollection();
            services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                    ["Fitz:ApplicationName"] = "compliance",
                    ["Compliance:Applications:GovernedRevisionsEnabled"] = enabled.ToString(),
                }).Build(), developerAuthentication: true);
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            services.AddSingleton<TimeProvider>(new FixedClock());
            services.AddSingleton<IKvClient>(new InMemoryKvClient());
            services.AddSingleton<IPermissionAuthorizer>(Permissions);
            services.AddSingleton<ITenantActivity>(new ActiveTenant());
            Memberships = new(Tenant, User);
            services.AddSingleton<ITenantMembershipDirectoryReader>(Memberships);
            var reader = services.Last(descriptor => descriptor.ServiceType == typeof(IAggregateReader));
            services.Remove(reader);
            services.AddScoped<IAggregateReader>(provider => new ObservedReader(
                (IAggregateReader)CreateService(reader, provider)));
            var executor = services.Last(descriptor => descriptor.ServiceType == typeof(IAggregateExecutor));
            services.Remove(executor);
            services.AddScoped<IAggregateExecutor>(provider => new TransitionExecutor(
                (IAggregateExecutor)CreateService(executor, provider),
                (ObservedReader)provider.GetRequiredService<IAggregateReader>()));
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
        }

        public async Task SeedAsync(bool complete = false, Uuid? systemOwner = null, Uuid? accessOwner = null)
        {
            var application = new DeclaredApplication(Tenant, Application);
            Assert.True(application.Declare("Legacy", "Legacy purpose", null, User,
                "Original", Now, complete ? "internal" : null,
                complete ? systemOwner ?? Owner : null, complete ? accessOwner ?? Owner : null,
                applicationKind: complete ? "service" : null,
                criticality: complete ? "critical" : null).IsSuccess);
            await Writer.SaveAsync(application, new RequestDispatchContext(RequestActor.System),
                CancellationToken.None);
            var foreign = new DeclaredApplication(ForeignTenant, ForeignApplication);
            Assert.True(foreign.Declare("Foreign", "Foreign purpose", null, User, "Foreign original", Now).IsSuccess);
            await Writer.SaveAsync(foreign, new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        }

        public async Task RecordPersonAsync(Uuid personId, Uuid? tenant = null)
        {
            var person = new Person(tenant ?? Tenant, personId);
            Assert.True(person.Record("Owner", null, ActorReference.ForMember(User, "Manager"), Now).IsSuccess);
            await Writer.SaveAsync(person, new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        }

        public ReviseApplication Request() => new(Tenant, Application, 1,
            "Revised", "Revised purpose", null);

        public ReviseApplication CompleteRequest() => Request() with
        {
            Name = " Revised ",
            Purpose = " Revised purpose ",
            OwnerReference = " Attribution ",
            Classification = " internal ",
            SystemOwnerPersonId = Owner,
            AccessOwnerPersonId = Owner,
            ApplicationKind = " service ",
            Criticality = " critical ",
        };

        public Task<DomainEventRecord[]> EventsAsync() => EventsAsync(Tenant, Application);
        public Task<DomainEventRecord[]> ForeignEventsAsync() => EventsAsync(ForeignTenant, ForeignApplication);

        async Task<DomainEventRecord[]> EventsAsync(Uuid tenant, Uuid application)
        {
            var records = new List<DomainEventRecord>();
            await foreach (var record in Events.ReadAsync(new EventStreamAddress(tenant.ToString(),
                               "applications", application.ToString()), 0, CancellationToken.None))
                records.Add(record);
            return records.ToArray();
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }

        static object CreateService(ServiceDescriptor descriptor, IServiceProvider provider) =>
            descriptor.ImplementationInstance ?? descriptor.ImplementationFactory?.Invoke(provider) ??
            ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!);
    }

    sealed class ObservedReader(IAggregateReader inner) : IAggregateReader
    {
        public int PersonReads { get; private set; }
        public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            var result = await inner.HydrateAsync(aggregate, ct);
            if (aggregate is Person)
                PersonReads++;
            return result;
        }
    }

    sealed class TransitionExecutor(IAggregateExecutor inner, ObservedReader reader) : IAggregateExecutor
    {
        public Func<Task>? BeforeApplicationExecution { get; set; }
        public bool Transitioned { get; private set; }
        public int ValidatedOwnerReads { get; private set; }

        public async ValueTask<Result> ExecuteAsync<TAggregate>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome> operation, IExecutionContext context,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            if (aggregate is DeclaredApplication && BeforeApplicationExecution is { } transition)
            {
                BeforeApplicationExecution = null;
                ValidatedOwnerReads = reader.PersonReads;
                await transition();
                Transitioned = true;
            }
            return await inner.ExecuteAsync(aggregate, operation, context, ct);
        }

        public ValueTask<Result<TOut>> ExecuteAsync<TAggregate, TOut>(TAggregate aggregate,
            Func<TAggregate, AggregateOutcome<TOut>> operation, IExecutionContext context,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            inner.ExecuteAsync(aggregate, operation, context, ct);
    }

    sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now.AddMinutes(1);
    }

    sealed class ActiveTenant : ITenantActivity
    {
        public ValueTask<bool> IsActiveAsync(Uuid tenantId, CancellationToken ct = default) =>
            ValueTask.FromResult(true);
    }
}

using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Workforce;

public sealed class RecordWorkRelationshipHandlerTests
{
    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    public async Task ShouldDenyEveryRestrictedGuessGivenHydratedRetryWithoutEitherFieldGrant(
        bool managerGrant, bool personalGrant, bool initiallyNull)
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var userId = Uuid.CreateVersion4();
        var initial = scenario.Request(initiallyNull ? null : scenario.ManagerId,
            initiallyNull ? null : "retirement");
        Assert.True((await scenario.RecordAsync(initial, new GrantedPermissions(), userId: userId)).IsSuccess);
        var permissions = new GrantedPermissions(managerGrant, personalGrant);
        var guesses = new[]
        {
            initial with { SourceWorkerId = " e-100 " },
            initial with { ManagerPersonId = scenario.OtherManagerId },
            initial with { EmploymentStatusReason = "resignation" },
            initial with { ManagerPersonId = null, EmploymentStatusReason = null },
        };

        // Act
        var results = new List<Result<WorkRelationshipRegistration>>();
        foreach (var guess in guesses)
            results.Add(await scenario.RecordAsync(guess, permissions, userId: userId));

        // Assert
        Assert.False(results[0].IsSuccess,
            $"A matching restricted guess succeeded; changed manager/reason guesses returned " +
            $"{results[1].Error?.Kind}/{results[2].Error?.Kind}.");
        var denied = Assert.IsType<RequestError>(results[0].Error);
        Assert.Equal(RequestErrorKind.Forbidden, denied.Kind);
        Assert.All(results, result =>
        {
            var error = Assert.IsType<RequestError>(result.Error);
            Assert.Equal(denied.Kind, error.Kind);
            Assert.Equal(denied.Message, error.Message);
            Assert.Equal(denied.IsTransient, error.IsTransient);
        });
        Assert.Single(await scenario.RelationshipEventsAsync());
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    public async Task ShouldCreateRelationshipGivenWorkforceManageWithoutBothReadGrants(
        bool managerGrant, bool personalGrant, bool initiallyNull)
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var request = scenario.Request(initiallyNull ? null : scenario.ManagerId,
            initiallyNull ? null : "retirement");

        // Act
        var result = await scenario.RecordAsync(request,
            new GrantedPermissions(managerGrant, personalGrant));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(WorkRelationship.IdFor(scenario.TenantId, request.SourceWorkerId),
            result.Value.RelationshipId);
        var recorded = Assert.IsType<WorkRelationshipRecorded>(
            Assert.Single(await scenario.RelationshipEventsAsync()));
        Assert.Equal(request.ManagerPersonId, recorded.Terms.ManagerPersonId);
        Assert.Equal(request.EmploymentStatusReason, recorded.Terms.EmploymentStatusReason);
    }

    [Fact]
    public async Task ShouldPreserveInitialAttributionAndConflictsGivenBothGrantsAfterRevision()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var initial = scenario.Request(scenario.ManagerId, "retirement");
        Assert.True((await scenario.RecordAsync(initial, new GrantedPermissions())).IsSuccess);
        await scenario.ReviseAsync(initial with
        {
            Department = "Operations",
            ManagerPersonId = scenario.OtherManagerId,
            EmploymentStatusReason = "transfer",
        });
        var before = await scenario.RelationshipEventsAsync();
        var original = Assert.IsType<WorkRelationshipRecorded>(before[0]);
        var permissions = new GrantedPermissions(true, true);

        // Act
        var identical = await scenario.RecordAsync(initial with
        {
            SourceWorkerId = " e-100 ",
            Department = " Engineering ",
        }, permissions);
        var publicChange = await scenario.RecordAsync(initial with { Department = "Finance" }, permissions);
        var managerChange = await scenario.RecordAsync(initial with
        {
            ManagerPersonId = scenario.OtherManagerId,
        }, permissions);
        var reasonChange = await scenario.RecordAsync(initial with
        {
            EmploymentStatusReason = "resignation",
        }, permissions);
        var after = await scenario.RelationshipEventsAsync();

        // Assert
        Assert.True(identical.IsSuccess);
        Assert.Equal(original.RelationshipId, identical.Value.RelationshipId);
        Assert.All(new[] { publicChange, managerChange, reasonChange }, result =>
            Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(result.Error).Kind));
        Assert.Equal(2, after.Count);
        var retained = Assert.IsType<WorkRelationshipRecorded>(after[0]);
        Assert.Equal(original.Actor, retained.Actor);
        Assert.Equal(original.ChangedAt, retained.ChangedAt);
        Assert.Equal(original.Metadata, retained.Metadata);
        Assert.Equal(2, Assert.IsType<WorkRelationshipRevised>(after[1]).Revision);
    }

    [Fact]
    public async Task ShouldHideRelationshipGivenActorOutsideRequestedTenant()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var initial = scenario.Request(scenario.ManagerId, "retirement");
        Assert.True((await scenario.RecordAsync(initial, new GrantedPermissions())).IsSuccess);
        var foreign = initial with { TenantId = Uuid.CreateVersion4() };

        // Act
        var matching = await scenario.RecordAsync(foreign,
            new GrantedPermissions(true, true), tenantMember: false);
        var changed = await scenario.RecordAsync(foreign with
        {
            ManagerPersonId = null,
            EmploymentStatusReason = null,
        }, new GrantedPermissions(true, true), tenantMember: false);

        // Assert
        var matchingError = Assert.IsType<RequestError>(matching.Error);
        var changedError = Assert.IsType<RequestError>(changed.Error);
        Assert.Equal(RequestErrorKind.NotFound, matchingError.Kind);
        Assert.Equal(matchingError.Message, changedError.Message);
        Assert.Equal(RequestErrorKind.NotFound, changedError.Kind);
        Assert.Single(await scenario.RelationshipEventsAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldDenyEveryGuessGivenConcurrentCreationBeforeExecutorHydration(
        bool changedGuess)
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var winner = scenario.Request(scenario.ManagerId, "retirement");
        var submitted = changedGuess ? winner with
        {
            ManagerPersonId = scenario.OtherManagerId,
            EmploymentStatusReason = "resignation",
        } : winner;
        var reader = new ConcurrentCreationReader(scenario.Reader, async () =>
            Assert.True((await scenario.RecordAsync(winner, new GrantedPermissions())).IsSuccess),
            beforeHydration: true);
        var executor = new AggregateExecutor(reader, scenario.Writer);

        // Act
        var result = await scenario.RecordAsync(submitted, new GrantedPermissions(), executor);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Single(await scenario.RelationshipEventsAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldConflictWithoutComparingWinnerGivenCreationAfterEmptyHydration(
        bool changedGuess)
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var winner = scenario.Request(scenario.ManagerId, "retirement");
        var submitted = changedGuess ? winner with
        {
            ManagerPersonId = scenario.OtherManagerId,
            EmploymentStatusReason = "resignation",
        } : winner;
        var reader = new ConcurrentCreationReader(scenario.Reader, async () =>
            Assert.True((await scenario.RecordAsync(winner, new GrantedPermissions())).IsSuccess));
        var executor = new AggregateExecutor(reader, scenario.Writer);

        // Act
        var conflict = await Assert.ThrowsAsync<EventStreamConcurrencyException>(() =>
            scenario.RecordAsync(submitted, new GrantedPermissions(), executor));
        var retry = await scenario.RecordAsync(submitted, new GrantedPermissions());

        // Assert
        Assert.NotNull(conflict);
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(retry.Error).Kind);
        var recorded = Assert.IsType<WorkRelationshipRecorded>(
            Assert.Single(await scenario.RelationshipEventsAsync()));
        Assert.Equal(winner.ManagerPersonId, recorded.Terms.ManagerPersonId);
        Assert.Equal(winner.EmploymentStatusReason, recorded.Terms.EmploymentStatusReason);
    }

    sealed class Scenario : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;

        Scenario(ServiceProvider provider)
        {
            _provider = provider;
            _scope = provider.CreateAsyncScope();
        }

        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid PersonId { get; } = Uuid.CreateVersion4();
        public Uuid ManagerId { get; } = Uuid.CreateVersion4();
        public Uuid OtherManagerId { get; } = Uuid.CreateVersion4();
        public IAggregateReader Reader => _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        public IAggregateWriter Writer => _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        IAggregateExecutor Executor => _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        IEventStore Events => _scope.ServiceProvider.GetRequiredService<IEventStore>();

        public static async Task<Scenario> CreateAsync()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            services.AddSingleton(TimeProvider.System);
            services.AddPortia();
            var scenario = new Scenario(services.BuildServiceProvider(
                new ServiceProviderOptions { ValidateScopes = true }));
            foreach (var id in new[] { scenario.PersonId, scenario.ManagerId, scenario.OtherManagerId })
            {
                var person = new Person(scenario.TenantId, id);
                Assert.True(person.Record("Recorded person", null,
                    ActorReference.ForMember(Uuid.CreateVersion4(), "Author"),
                    DateTimeOffset.UtcNow).IsSuccess);
                await scenario.Writer.SaveAsync(person, new RequestDispatchContext(RequestActor.System));
            }
            return scenario;
        }

        public RecordWorkRelationship Request(Uuid? managerId, string? reason) => new(
            TenantId, PersonId, "E-100", "employee", "active", new DateOnly(2025, 1, 6),
            Department: "Engineering", ManagerPersonId: managerId, EmploymentStatusReason: reason);

        public async Task<Result<WorkRelationshipRegistration>> RecordAsync(
            RecordWorkRelationship request, IPermissionAuthorizer permissions,
            IAggregateExecutor? executor = null, bool tenantMember = true, Uuid? userId = null)
        {
            var actor = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("iss", "bdgrz"), new Claim("sub", (userId ?? Uuid.CreateVersion4()).ToString())],
                "BdgrzSession"));
            var authorized = await new WorkforceAuthorizer(new FixedMembershipDirectory(tenantMember),
                    new ActiveTenant(), permissions)
                .AuthorizeAsync(new RequestContext<IWorkforceRequest>(request, actor),
                    CancellationToken.None);
            if (!authorized.IsSuccess)
                return Result<WorkRelationshipRegistration>.Failure(authorized.Error);
            return await new RecordWorkRelationshipHandler(executor ?? Executor, Reader,
                    permissions, TimeProvider.System)
                .HandleAsync(new RequestContext<RecordWorkRelationship>(request, actor),
                    CancellationToken.None);
        }

        public async Task ReviseAsync(RecordWorkRelationship terms)
        {
            var relationship = await Reader.HydrateAsync(new WorkRelationship(TenantId,
                WorkRelationship.IdFor(TenantId, terms.SourceWorkerId)));
            Assert.Null(relationship.Revise(relationship.Revision,
                new WorkRelationshipTerms(terms.WorkerType, terms.LifecycleStatus, terms.StartDate,
                    terms.EndDate, terms.Department, terms.ManagerPersonId, terms.SponsorPersonId,
                    terms.EmploymentStatusReason),
                ActorReference.ForMember(Uuid.CreateVersion4(), "Editor"), DateTimeOffset.UtcNow));
            await Writer.SaveAsync(relationship, new RequestDispatchContext(RequestActor.System));
        }

        public async Task<List<DomainEvent>> RelationshipEventsAsync()
        {
            var events = new List<DomainEvent>();
            await foreach (var record in Events.ReadAsync(new WorkRelationship(TenantId,
                               WorkRelationship.IdFor(TenantId, "E-100")).Stream,
                               0, CancellationToken.None))
                events.Add(record.Event);
            return events;
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class ConcurrentCreationReader(IAggregateReader inner, Func<Task> create,
        bool beforeHydration = false)
        : IAggregateReader
    {
        bool _created;

        public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            if (!_created && beforeHydration && aggregate is WorkRelationship)
            {
                _created = true;
                await create();
            }
            var hydrated = await inner.HydrateAsync(aggregate, ct);
            if (!_created && hydrated is WorkRelationship { IsCreated: false })
            {
                _created = true;
                await create();
            }
            return hydrated;
        }
    }

    sealed class GrantedPermissions(bool manager = false, bool personal = false) : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            string permission, CancellationToken ct = default) => ValueTask.FromResult(
            permission == RbacPermissions.WorkforceManage ||
            manager && permission == FieldClasses.WorkforceManagerChain.ReadPermission ||
            personal && permission == FieldClasses.WorkforcePersonalDetails.ReadPermission);
    }
}

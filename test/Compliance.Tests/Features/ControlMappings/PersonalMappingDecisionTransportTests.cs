using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.ControlMappings;
using Bdgrz.Compliance.Features.Criteria;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.ControlMappings;

public sealed class PersonalMappingDecisionTransportTests
{
    [Theory]
    [InlineData("proposal", "direct")]
    [InlineData("proposal", "mcp")]
    [InlineData("applicability", "direct")]
    [InlineData("applicability", "mcp")]
    [InlineData("mapping", "direct")]
    [InlineData("mapping", "mcp")]
    [InlineData("proposal", "http")]
    [InlineData("applicability", "http")]
    [InlineData("mapping", "http")]
    public async Task ShouldEnforcePersonalTransportGivenValidMappingDecision(string action, string transport)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(action);
        var before = await fixture.PositionAsync(action);

        // Act
        var result = await fixture.DecideAsync(action, transport);
        var after = await fixture.PositionAsync(action);

        // Assert
        if (transport == "http")
        {
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Equal(before + 1, after);
            var actor = await fixture.LastActorAsync(action);
            Assert.Equal(RbacIds.Member(fixture.Tenant, action == "proposal" ? fixture.Author : fixture.Reviewer).ToString(), actor.Id);
        }
        else
        {
            Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
            Assert.Contains("personal HTTP", result.Error!.Message, StringComparison.Ordinal);
            Assert.Equal(before, after);
        }
    }

    [Theory]
    [InlineData("applicability", "A not-applicable proposer cannot review their own proposal.")]
    [InlineData("mapping", "A mapping proposer cannot review their own proposal.")]
    public async Task ShouldPreserveProposerSeparationGivenNativeHttpSelfReview(string action, string message)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(action);
        var before = await fixture.PositionAsync(action);

        // Act
        var result = await fixture.DecideAsync(action, "http", fixture.Author);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal(message, result.Error!.Message);
        Assert.Equal(before, await fixture.PositionAsync(action));
    }

    [Theory]
    [InlineData("proposal", "A not-applicable decision must target the criteria edition the program has selected.")]
    [InlineData("applicability", "The decision targets an edition the program no longer selects.")]
    [InlineData("mapping", "The mapping targets an edition the program no longer selects; remap it explicitly.")]
    public async Task ShouldPreserveSelectedEditionGivenNativeHttpAfterProgramChange(string action, string message)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(action);
        await fixture.ChangeEditionAsync();
        var before = await fixture.PositionAsync(action);

        // Act
        var result = await fixture.DecideAsync(action, "http");

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(message, result.Error!.Message);
        Assert.Equal(before, await fixture.PositionAsync(action));
    }

    [Fact]
    public async Task ShouldPreserveCatalogueCriterionGivenNativeHttpPointOfFocusProposal()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync("proposal");

        // Act
        var result = await fixture.DecideAsync("proposal", "http", criterion: "bdgrz:focus:cc6.1-access");

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal("Not-applicable decisions apply to a criterion of the selected edition, not a point of focus.", result.Error!.Message);
        Assert.Equal(0UL, await fixture.PositionAsync("proposal"));
    }

    [Theory]
    [InlineData("proposal")]
    [InlineData("applicability")]
    [InlineData("mapping")]
    public async Task ShouldPreserveSourceGrantGivenNativeHttpWithoutManagement(string action)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(action);
        fixture.Permissions.Allowed = false;
        var before = await fixture.PositionAsync(action);

        // Act
        var result = await fixture.DecideAsync(action, "http");

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal("The actor may not manage this program.", result.Error!.Message);
        Assert.Equal(before, await fixture.PositionAsync(action));
    }

    [Theory]
    [InlineData("proposal")]
    [InlineData("applicability")]
    [InlineData("mapping")]
    public async Task ShouldPreserveOwningProgramGivenNativeHttpWithForeignProgram(string action)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(action);
        var before = await fixture.PositionAsync(action);

        // Act
        var result = await fixture.DecideAsync(action, "http", program: Uuid.CreateVersion4());

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Equal("The program was not found.", result.Error!.Message);
        Assert.Equal(before, await fixture.PositionAsync(action));
    }

    internal static async Task<Result> HttpAsync(IServiceProvider provider, Uuid user, IRequest request, RequestErrorKind? expectedFailure = null)
    {
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request, Context(user, "http"), CancellationToken.None);
        if (expectedFailure is { } kind)
            Assert.Equal(kind, result.Error?.Kind);
        else
            Assert.True(result.IsSuccess, result.Error?.Message);
        return result;
    }
    internal static async Task<Result<T>> HttpAsync<T>(IServiceProvider provider, Uuid user, IRequest<T> request, RequestErrorKind? expectedFailure = null)
    {
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request, Context(user, "http"), CancellationToken.None);
        if (expectedFailure is { } kind)
            Assert.Equal(kind, result.Error?.Kind);
        else
            Assert.True(result.IsSuccess, result.Error?.Message);
        return result;
    }
    internal static RequestDispatchContext Context(Uuid user, string transport) => new(ProgramManagementServices.Actor(user),
        transport == "http" ? new HttpInvocation("POST", "/synthetic/mapping/decision", "/synthetic/mapping/decision", "synthetic") :
        transport == "mcp" ? new McpInvocation("synthetic.mapping.decision") : new DirectInvocation());

    sealed class Permissions : IPermissionAuthorizer
    {
        public bool Allowed { get; set; } = true;
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(Allowed);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        static readonly Uuid Edition = Uuid.CreateVersion5(Uuid.Empty, "mapping-test-current");
        static readonly Uuid NextEdition = Uuid.CreateVersion5(Uuid.Empty, "mapping-test-next");
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid Program { get; } = Uuid.CreateVersion4();
        public Uuid Author { get; } = Uuid.CreateVersion4();
        public Uuid Reviewer { get; } = Uuid.CreateVersion4();
        Uuid _decision;
        public Permissions Permissions { get; } = new();

        Fixture(ControlCriterionMappingHandlerTests.Fixture source)
        {
            Tenant = source.TenantId;
            Program = source.ProgramId;
            Author = source.AuthorUserId;
            Reviewer = source.ReviewerUserId;
            var services = new ServiceCollection();
            services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance"
            }).Build(), developerAuthentication: true);
            var events = source.Provider.GetRequiredService<IEventStore>();
            services.AddSingleton<IEventStore>(events);
            services.AddSingleton<IDomainEventReader>((IDomainEventReader)events);
            services.AddSingleton<IKvClient>(new InMemoryKvClient());
            services.AddSingleton<IAccessGrantPermissionAuthorizer>(new PermissionBackedAccessGrantPermissionAuthorizer(Permissions));
            services.AddSingleton<ITenantActivity, ActiveTenant>();
            services.AddSingleton<ITenantMembershipDirectoryReader, AlwaysMemberDirectory>();
            services.AddSingleton<ICriteriaCatalog>(ControlCriterionMappingHandlerTests.TestCatalog());
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        }
        public static async Task<Fixture> CreateAsync(string action)
        {
            var source = await ControlCriterionMappingHandlerTests.Fixture.CreateAsync();
            var fixture = new Fixture(source);
            await source.Provider.DisposeAsync();
            await using var scope = fixture._provider.CreateAsyncScope();
            var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
            if (action == "mapping")
            {
                var proposed = await bus.DispatchAsync(new ProposeControlCriterionMapping(fixture.Tenant, fixture.Program,
                    source.ControlId, source.ControlVersionId, Edition, "CC6.1", 0, "Access is reviewed", "Production scope"),
                    Context(fixture.Author, "direct"), CancellationToken.None);
                Assert.True(proposed.IsSuccess, proposed.Error?.Message);
                fixture._decision = proposed.Value.MappingId;
            }
            else if (action == "applicability")
            {
                var proposed = await bus.DispatchAsync(new ProposeCriterionNotApplicable(fixture.Tenant, fixture.Program,
                    Edition, "CC6.2", 0, "Not applicable"), Context(fixture.Author, "http"), CancellationToken.None);
                Assert.True(proposed.IsSuccess, proposed.Error?.Message);
                fixture._decision = proposed.Value.DecisionId;
            }
            return fixture;
        }
        public Task ChangeEditionAsync() => ProgramManagementServices.SeedAsync(_provider, new ComplianceProgram(Tenant, Program),
            program => { Assert.Null(program.SelectCriteriaEdition(2, NextEdition, Author, "Author", DateTimeOffset.UtcNow)); return Result.Success; });
        public async Task<ulong> PositionAsync(string action) => action == "mapping"
            ? (await ProgramManagementServices.HydrateAsync(_provider, new ControlCriterionMappingLedger(Tenant, Program))).CommittedStreamPosition
            : (await ProgramManagementServices.HydrateAsync(_provider, new CriterionApplicabilityLedger(Tenant, Program))).CommittedStreamPosition;
        public async Task<ActorReference> LastActorAsync(string action)
        {
            ActorReference? actor = null;
            var pattern = EventStreamPattern.ForPattern(Tenant.ToString(), action == "mapping" ? "control-criterion-mappings" : "criterion-applicability");
            await foreach (var record in _provider.GetRequiredService<IDomainEventReader>().ReadAsync(pattern, EventCursor.Start, CancellationToken.None))
                actor = record.Event switch
                {
                    CriterionNotApplicableProposed proposed => proposed.Actor,
                    CriterionApplicabilityReviewed reviewed => reviewed.Actor,
                    ControlCriterionMappingReviewed reviewed => reviewed.Actor,
                    _ => actor
                };
            return Assert.IsType<ActorReference>(actor);
        }
        public async Task<Result> DecideAsync(string action, string transport, Uuid? user = null, string criterion = "CC6.2", Uuid? program = null)
        {
            await using var scope = _provider.CreateAsyncScope();
            var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
            var context = Context(user ?? (action == "proposal" ? Author : Reviewer), transport);
            if (action == "proposal")
            {
                var result = await bus.DispatchAsync(new ProposeCriterionNotApplicable(Tenant, program ?? Program, Edition, criterion, 0, "Not applicable"), context, CancellationToken.None);
                return result.IsSuccess ? Result.Success : Result.Failure(result.Error);
            }
            IRequest request = action == "mapping" ? new ReviewControlCriterionMapping(Tenant, program ?? Program, _decision, 1, "accept", "Verified") :
                new ReviewCriterionApplicability(Tenant, program ?? Program, _decision, 1, "accept", "Verified");
            return await bus.DispatchAsync(request, context, CancellationToken.None);
        }
        public ValueTask DisposeAsync() => _provider.DisposeAsync();
    }
}

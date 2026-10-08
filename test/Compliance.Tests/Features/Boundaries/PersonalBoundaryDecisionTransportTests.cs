using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Boundaries;

public sealed class PersonalBoundaryDecisionTransportTests
{
    [Theory]
    [InlineData("review", "direct")]
    [InlineData("review", "mcp")]
    [InlineData("approval", "direct")]
    [InlineData("approval", "mcp")]
    [InlineData("review", "http")]
    [InlineData("approval", "http")]
    public async Task ShouldRequirePersonalSubmissionGivenValidBoundaryDecision(string action, string transport)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(action);
        var before = await fixture.ReadAsync();

        // Act
        var result = await fixture.DecideAsync(action, transport);
        var after = await fixture.ReadAsync();

        // Assert
        if (transport == "http")
        {
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Equal(before.CommittedStreamPosition + 1, after.CommittedStreamPosition);
            await fixture.AssertAttributionAsync(action);
        }
        else
        {
            Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
            Assert.Contains("personal HTTP", result.Error!.Message, StringComparison.Ordinal);
            Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
        }
    }

    [Theory]
    [InlineData("review", "author", RequestErrorKind.Forbidden, "A boundary author cannot review their own draft.")]
    [InlineData("approval", "author", RequestErrorKind.Forbidden, "A boundary author cannot approve their own draft.")]
    [InlineData("review", "stale", RequestErrorKind.Conflict, "The boundary draft changed. Reload it before review.")]
    [InlineData("approval", "stale", RequestErrorKind.Conflict, "The boundary draft changed or its projection has not reached the requested revision.")]
    [InlineData("approval", "review", RequestErrorKind.Conflict, "Approval requires the latest accepted review of this draft revision.")]
    [InlineData("approval", "digest", RequestErrorKind.Conflict, "The impact preview changed. Reload it before approval.")]
    public async Task ShouldPreserveSourceRefusalGivenNativeHttpBoundaryDecision(string action, string fault, RequestErrorKind kind, string message)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(action);
        var before = await fixture.ReadAsync();

        // Act
        var result = await fixture.DecideAsync(action, "http", fault == "author", fault == "stale", fault == "review", fault == "digest");

        // Assert
        Assert.Equal(kind, result.Error?.Kind);
        Assert.Equal(message, result.Error?.Message);
        Assert.Equal(before.CommittedStreamPosition, (await fixture.ReadAsync()).CommittedStreamPosition);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly Uuid _tenant = Uuid.CreateVersion4();
        readonly Uuid _program = Uuid.CreateVersion4();
        readonly Uuid _author = Uuid.CreateVersion4();
        readonly Uuid _reviewer = Uuid.CreateVersion4();
        readonly Uuid _approver = Uuid.CreateVersion4();
        readonly Uuid _boundary = Uuid.CreateVersion4();
        readonly Uuid _version = Uuid.CreateVersion4();
        readonly Uuid _review = Uuid.CreateVersion4();
        string _digest = string.Empty;

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
            services.AddSingleton<IDomainEventReader>(events);
            services.AddSingleton<IKvClient>(new InMemoryKvClient());
            services.AddSingleton<IAccessGrantPermissionAuthorizer>(new PermissionBackedAccessGrantPermissionAuthorizer(new RecordingPermissionAuthorizer(true)));
            services.AddSingleton<ITenantActivity, ActiveTenant>();
            services.AddSingleton<ITenantMembershipDirectoryReader, AlwaysMemberDirectory>();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        }

        public static async Task<Fixture> CreateAsync(string action)
        {
            var fixture = new Fixture();
            var now = DateTimeOffset.UtcNow;
            var author = RbacIds.Member(fixture._tenant, fixture._author);
            await ProgramManagementServices.SeedAsync(fixture._provider, new ComplianceProgram(fixture._tenant, fixture._program), program =>
            {
                Assert.Null(program.Create("Security", new ProgramPlan(null, null, null, null, null, null), author, "Author", now));
                return Result.Success;
            });
            await ProgramManagementServices.SeedAsync(fixture._provider, new SystemBoundary(fixture._tenant, fixture._boundary), boundary =>
                boundary.Create(fixture._program, fixture._version, new BoundaryContent("Service scope", "readiness", ["security"], []), author, "Author", now));
            if (action == "approval")
            {
                Assert.True((await fixture.DecideAsync("review", "http")).IsSuccess);
                await using var scope = fixture._provider.CreateAsyncScope();
                var directory = (FitzBoundaryDirectory)scope.ServiceProvider.GetRequiredService<IBoundaryDirectoryProjection>();
                var identity = new CheckpointIdentity("BoundaryDirectoryV2", EventStreamPattern.ForPattern(fixture._tenant.ToString()));
                await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
                {
                    await foreach (var record in fixture._provider.GetRequiredService<IDomainEventReader>().ReadAsync(EventStreamPattern.ForPattern(fixture._tenant.ToString()), EventCursor.Start, CancellationToken.None))
                        await directory.ApplyAsync(record.Event);
                    await batch.CommitAsync(ProjectionCheckpoint.Start);
                }
                var preview = await scope.ServiceProvider.GetRequiredService<BoundaryImpactService>().PreviewAsync(new PreviewBoundaryImpact(fixture._tenant, fixture._boundary, fixture._version, 1), CancellationToken.None);
                Assert.True(preview.IsSuccess, preview.Error?.Message);
                Assert.True(preview.Value.Complete);
                fixture._digest = preview.Value.Digest;
            }
            return fixture;
        }

        public async Task<Result> DecideAsync(string action, string transport, bool author = false, bool stale = false, bool wrongReview = false, bool wrongDigest = false)
        {
            IRequest request = action == "review" ? new ReviewBoundary(_tenant, _boundary, _version, stale ? 2 : 1, "accept", "Independent review") :
                new ApproveBoundary(_tenant, _boundary, _version, stale ? 2 : 1, wrongReview ? Uuid.CreateVersion4() : _review, new DateOnly(2026, 10, 1), "Approved", wrongDigest ? "STALE" : _digest);
            RequestInvocation invocation = transport == "http" ? new HttpInvocation("POST", "/synthetic/boundary/decision", "/synthetic/boundary/decision", "synthetic") :
                transport == "mcp" ? new McpInvocation("synthetic.boundary.decision") : new DirectInvocation();
            await using var scope = _provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
                new RequestDispatchContext(ProgramManagementServices.Actor(author ? _author : action == "review" ? _reviewer : _approver), invocation,
                    new RequestMetadata(action == "review" ? _review : Uuid.CreateVersion4(), _review, null)), CancellationToken.None);
        }

        public async Task AssertAttributionAsync(string action)
        {
            DomainEvent? last = null;
            await foreach (var record in _provider.GetRequiredService<IDomainEventReader>().ReadAsync(EventStreamPattern.ForPattern(_tenant.ToString()), EventCursor.Start, CancellationToken.None))
                if (record.Event is BoundaryReviewed or BoundaryApproved)
                    last = record.Event;
            if (action == "review")
            {
                var review = Assert.IsType<BoundaryReviewed>(last);
                Assert.Equal(RbacIds.Member(_tenant, _reviewer).ToString(), review.Actor.Id);
                Assert.Equal(_version, review.DraftVersionId);
                Assert.Equal(1, review.Revision);
                Assert.Equal(_review, review.DecisionId);
            }
            else
            {
                var approval = Assert.IsType<BoundaryApproved>(last);
                Assert.Equal(RbacIds.Member(_tenant, _approver).ToString(), approval.Actor.Id);
                Assert.Equal(_version, approval.DraftVersionId);
                Assert.Equal(1, approval.Revision);
                Assert.Equal(_review, approval.AcceptedReviewDecisionId);
                Assert.Equal(_digest, approval.ImpactDigest);
                Assert.True((await ReadAsync()).IsVersionApproved(_version));
            }
        }

        public Task<SystemBoundary> ReadAsync() => ProgramManagementServices.HydrateAsync(_provider, new SystemBoundary(_tenant, _boundary));
        public ValueTask DisposeAsync() => _provider.DisposeAsync();
    }
}

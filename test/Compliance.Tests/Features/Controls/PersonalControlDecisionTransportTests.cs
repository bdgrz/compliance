using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Controls;

public sealed class PersonalControlDecisionTransportTests
{
    [Theory]
    [InlineData("review", "direct")]
    [InlineData("review", "mcp")]
    [InlineData("approval", "direct")]
    [InlineData("approval", "mcp")]
    [InlineData("retirement", "direct")]
    [InlineData("retirement", "mcp")]
    public async Task ShouldRefusePersonalControlDecisionGivenNonHttpInvocation(string action, string transport)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(action);
        var before = await fixture.ReadAsync();

        // Act
        var result = await fixture.DecideAsync(action, transport);
        var after = await fixture.ReadAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Contains("personal HTTP", result.Error!.Message, StringComparison.Ordinal);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
        Assert.Equal(before.Revision, after.Revision);
        Assert.Equal(before.ReadDecisions(), after.ReadDecisions());
    }

    [Theory]
    [InlineData("review")]
    [InlineData("approval")]
    [InlineData("retirement")]
    public async Task ShouldRetainAttributedControlDecisionGivenNativeHttp(string action)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(action);
        var before = await fixture.ReadAsync();

        // Act
        var result = await fixture.DecideAsync(action, "http");
        var after = await fixture.ReadAsync();

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(before.CommittedStreamPosition + 1, after.CommittedStreamPosition);
        var decision = after.ReadDecisions()[^1];
        Assert.Equal(RbacIds.Member(fixture.Tenant, action == "review" ? fixture.Reviewer : fixture.Approver).ToString(), decision.Actor.Id);
        Assert.Equal(before.PendingTargetId, decision.VersionId);
        Assert.Equal(before.Revision, decision.Revision);
        if (action != "review")
            Assert.Equal(fixture.AcceptedReview, decision.ReliesOnDecisionId);
        if (action == "approval")
            Assert.Equal(fixture.InitialVersion, after.ApprovedVersion!.VersionId);
        if (action == "retirement")
            Assert.Equal("retired", after.ApprovedVersion!.Status);
    }

    [Theory]
    [InlineData("review", "author", "A control draft or retirement author cannot review their own proposal.")]
    [InlineData("approval", "author", "A control draft or retirement author cannot approve their own proposal.")]
    [InlineData("retirement", "author", "A control draft or retirement author cannot approve their own proposal.")]
    [InlineData("review", "owner", "The member has a conflicting responsibility and requires an active exact-scope waiver.")]
    [InlineData("approval", "owner", "The member has a conflicting responsibility and requires an active exact-scope waiver.")]
    public async Task ShouldPreserveControlSeparationGivenNativeHttpConflictingActor(string action, string role, string message)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(action);
        var before = await fixture.ReadAsync();

        // Act
        var result = await fixture.DecideAsync(action, "http", actor: role == "author" ? fixture.Author : fixture.Owner);
        var after = await fixture.ReadAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal(message, result.Error?.Message);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    [Theory]
    [InlineData("approval", "Approval requires the latest accepted review of this exact draft revision.")]
    [InlineData("retirement", "Retirement requires the latest accepted review of this exact retirement proposal.")]
    public async Task ShouldPreserveExactAcceptedReviewGivenNativeHttpWrongDecision(string action, string message)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(action);
        var before = await fixture.ReadAsync();

        // Act
        var result = await fixture.DecideAsync(action, "http", review: Uuid.CreateVersion4());
        var after = await fixture.ReadAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(message, result.Error?.Message);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldPreserveImpactConfirmationGivenNativeHttpChangedDigest()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync("retirement");
        var before = await fixture.ReadAsync();

        // Act
        var result = await fixture.DecideAsync("retirement", "http", digest: "STALE");
        var after = await fixture.ReadAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal("The impact preview changed. Reload it before deciding.", result.Error?.Message);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldPreserveCurrentDraftRevisionGivenNativeHttpStaleApproval()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync("approval");
        var before = await fixture.ReadAsync();

        // Act
        var result = await fixture.DecideAsync("approval", "http", revision: 2);
        var after = await fixture.ReadAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal("The control draft changed. Current revision: 1. Reload it and retry.", result.Error?.Message);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldPreserveCurrentOwnerVerificationGivenNativeHttpSuspendedOwner()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync("approval");
        await fixture.SuspendOwnerAsync();
        var before = await fixture.ReadAsync();

        // Act
        var result = await fixture.DecideAsync("approval", "http");
        var after = await fixture.ReadAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal("Activation requires an active client-personnel member assigned control_owner, or a verified workforce person designated as owner, on this exact draft revision.", result.Error?.Message);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    internal static async Task<Result> HttpAsync(IServiceProvider provider, Uuid user, IRequest request,
        RequestErrorKind? expectedFailure = null, RequestMetadata? metadata = null)
    {
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
            new RequestDispatchContext(ProgramManagementServices.Actor(user), Invocation("http"), metadata), CancellationToken.None);
        if (expectedFailure is { } kind)
            Assert.Equal(kind, result.Error?.Kind);
        else
            Assert.True(result.IsSuccess, result.Error?.Message);
        return result;
    }

    static RequestInvocation Invocation(string transport) => transport switch
    {
        "direct" => new DirectInvocation(),
        "mcp" => new McpInvocation("synthetic.control.decision"),
        _ => new HttpInvocation("POST", "/synthetic/control/decision", "/synthetic/control/decision", "synthetic")
    };

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid Program { get; } = Uuid.CreateVersion4();
        public Uuid Author { get; } = Uuid.CreateVersion4();
        public Uuid Reviewer { get; } = Uuid.CreateVersion4();
        public Uuid Approver { get; } = Uuid.CreateVersion4();
        public Uuid Owner { get; } = Uuid.CreateVersion4();
        public Uuid Control => ControlDraft.IdFor(Tenant, Program, "AC-PERSONAL");
        public Uuid InitialVersion => ControlVersionIds.Initial(Control);
        public Uuid AcceptedReview { get; private set; }
        string _digest = string.Empty;
        long _revision = 1;

        Fixture()
        {
            var services = new ServiceCollection();
            services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance",
                ["Compliance:Controls:ActivationEnabled"] = "true",
                ["Compliance:Controls:LifecycleEnabled"] = "true"
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
            var author = RbacIds.Member(fixture.Tenant, fixture.Author);
            await ProgramManagementServices.SeedAsync(fixture._provider, new ComplianceProgram(fixture.Tenant, fixture.Program), program =>
            {
                Assert.Null(program.Create("Security", new ProgramPlan(null, null, null, null, null, null), author, "Author", now));
                return Result.Success;
            });
            await ProgramManagementServices.SeedAsync(fixture._provider, new Member(fixture.Tenant, fixture.Owner), member => member.Register());
            await ProgramManagementServices.SeedAsync(fixture._provider, new ControlDraft(fixture.Tenant, fixture.Control), control =>
            {
                Assert.True(control.Create(fixture.Program, Uuid.CreateVersion4(), "AC-PERSONAL", new ControlDraftContent(
                    "Access review", "Review access", "Management reviews access", "Quarterly access review.", ["Dated review record"]), author, "Author", now).IsSuccess);
                Assert.Null(control.AssignResponsibility(new ResponsibilityScope("control", fixture.Control, fixture.InitialVersion, 1),
                    Uuid.CreateVersion4(), RbacIds.Member(fixture.Tenant, fixture.Owner), ResponsibilityType.ControlOwner, author, "Author", now, now.AddMinutes(-1), null, []));
                return Result.Success;
            });
            if (action != "review")
                await fixture.ReviewHttpAsync();
            if (action == "retirement")
            {
                Assert.True((await fixture.DecideAsync("approval", "http")).IsSuccess);
                await using var scope = fixture._provider.CreateAsyncScope();
                var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
                var proposed = await bus.SendAsync(new ProposeControlRetirement(fixture.Tenant, fixture.Program, fixture.Control,
                    fixture.InitialVersion, new DateOnly(2027, 3, 1), "Replaced."), ProgramManagementServices.Actor(fixture.Author));
                Assert.True(proposed.IsSuccess, proposed.Error?.Message);
                fixture._revision = proposed.Value.Revision;
                await fixture.ReviewHttpAsync();
                var preview = await bus.SendAsync(new PreviewControlImpact(fixture.Tenant, fixture.Program, fixture.Control, fixture._revision), ProgramManagementServices.Actor(fixture.Approver));
                Assert.True(preview.IsSuccess, preview.Error?.Message);
                Assert.True(preview.Value.Complete);
                fixture._digest = preview.Value.Digest;
            }
            return fixture;
        }

        async Task ReviewHttpAsync()
        {
            AcceptedReview = Uuid.CreateVersion4();
            await HttpAsync(_provider, Reviewer, new ReviewControl(Tenant, Program, Control, _revision, "accept", "Independent review."),
                metadata: new RequestMetadata(AcceptedReview, AcceptedReview, null));
        }

        public async Task<Result> DecideAsync(string action, string transport, Uuid? actor = null, Uuid? review = null, string? digest = null, long? revision = null)
        {
            IRequest request = action switch
            {
                "review" => new ReviewControl(Tenant, Program, Control, _revision, "accept", "Independent review."),
                "approval" => new ApproveControl(Tenant, Program, Control, revision ?? _revision, review ?? AcceptedReview, new DateOnly(2026, 10, 1), "Ready to operate."),
                _ => new RetireControl(Tenant, Program, Control, _revision, review ?? AcceptedReview, digest ?? _digest, "Retire at quarter end.")
            };
            await using var scope = _provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
                new RequestDispatchContext(ProgramManagementServices.Actor(actor ?? (action == "review" ? Reviewer : Approver)), Invocation(transport)), CancellationToken.None);
        }

        public Task SuspendOwnerAsync() => ProgramManagementServices.SeedAsync(_provider, new Member(Tenant, Owner),
            member => member.Suspend(RbacIds.Member(Tenant, Author), "Author", DateTimeOffset.UtcNow, "Leave of absence."));

        public Task<ControlDraft> ReadAsync() => ProgramManagementServices.HydrateAsync(_provider, new ControlDraft(Tenant, Control));
        public ValueTask DisposeAsync() => _provider.DisposeAsync();
    }
}

using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static Bdgrz.Compliance.Tests.Features.Providers.AssuranceSamples;

namespace Bdgrz.Compliance.Tests.Features.Providers;

public sealed class PersonalProviderReviewTransportTests
{
    [Theory]
    [InlineData("direct")]
    [InlineData("mcp")]
    [InlineData("http")]
    public async Task ShouldEnforcePersonalTransportGivenValidProviderReview(string transport)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var before = await fixture.ReadAsync();

        // Act
        var result = await fixture.ReviewAsync(transport);
        var after = await fixture.ReadAsync();

        // Assert
        if (transport == "http")
        {
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Equal(before.CommittedStreamPosition + 1, after.CommittedStreamPosition);
            var review = Assert.Single(after.Reviews(fixture.ProviderId));
            Assert.Equal(RbacIds.Member(fixture.Tenant, fixture.User).ToString(), review.ReviewedBy.Id);
            Assert.Equal(1, review.ProviderRevision);
            Assert.Equal("acceptable", review.Content.Conclusion);
        }
        else
        {
            Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
            Assert.Contains("personal HTTP", result.Error!.Message, StringComparison.Ordinal);
            Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
            Assert.Empty(after.Reviews(fixture.ProviderId));
        }
    }

    [Theory]
    [InlineData("grant", RequestErrorKind.Forbidden, "The actor requires organization-wide provider authority.")]
    [InlineData("provider", RequestErrorKind.NotFound, "The provider was not found.")]
    [InlineData("report", RequestErrorKind.NotFound, "The assurance report was not found for this provider.")]
    [InlineData("artifact", RequestErrorKind.NotFound, "A governed artifact reference was not found in its owning context.")]
    public async Task ShouldPreserveProviderSourceAuthorityGivenNativeHttpInvalidInput(string refusal, RequestErrorKind kind, string message)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var before = await fixture.ReadAsync();
        if (refusal == "grant")
            fixture.Permissions.Allowed = false;

        // Act
        var result = await fixture.ReviewAsync("http", refusal);
        var after = await fixture.ReadAsync();

        // Assert
        Assert.Equal(kind, result.Error?.Kind);
        Assert.Equal(message, result.Error!.Message);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldRetainCurrentProviderAndReportRevisionGivenNativeHttpReview()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await fixture.ReviseSourcesAsync();

        // Act
        var result = await fixture.ReviewAsync("http", "current_report");
        var after = await fixture.ReadAsync();

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var review = Assert.Single(after.Reviews(fixture.ProviderId));
        Assert.Equal(2, review.ProviderRevision);
        Assert.Equal(2, review.AssuranceReportRevision);
        Assert.Equal(fixture.ReportId, review.Content.AssuranceReportId);
    }

    [Fact]
    public async Task ShouldRetainOneReviewGivenExactNativeHttpMetadataRetry()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var metadata = RequestMetadata.Create();

        // Act
        var first = await fixture.ReviewAsync("http", metadata: metadata);
        var repeated = await fixture.ReviewAsync("http", metadata: metadata);
        var after = await fixture.ReadAsync();

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(repeated.IsSuccess, repeated.Error?.Message);
        Assert.Equal(first.Value, repeated.Value);
        Assert.Equal(first.Value.ReviewId, Assert.Single(after.Reviews(fixture.ProviderId)).ReviewId);
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
    static RequestDispatchContext Context(Uuid user, string transport, RequestMetadata? metadata = null) => new(ProgramManagementServices.Actor(user),
        transport == "http" ? new HttpInvocation("POST", "/synthetic/provider/review", "/synthetic/provider/review", "synthetic") :
        transport == "mcp" ? new McpInvocation("synthetic.provider.review") : new DirectInvocation(), metadata);

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid User { get; } = Uuid.CreateVersion4();
        public Uuid ProviderId { get; private set; }
        public Uuid ReportId { get; private set; }
        public Permissions Permissions { get; } = new();
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
            services.AddSingleton<IAccessGrantPermissionAuthorizer>(new PermissionBackedAccessGrantPermissionAuthorizer(Permissions));
            services.AddSingleton<ITenantActivity, ActiveTenant>();
            services.AddSingleton<ITenantMembershipDirectoryReader, AlwaysMemberDirectory>();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        }
        public static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture();
            await using var scope = fixture._provider.CreateAsyncScope();
            var registered = await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(new RecordProvider(fixture.Tenant, MaterialProvider()), ProgramManagementServices.Actor(fixture.User));
            Assert.True(registered.IsSuccess, registered.Error?.Message);
            fixture.ProviderId = registered.Value.ProviderId;
            return fixture;
        }
        public async Task ReviseSourcesAsync()
        {
            await using var scope = _provider.CreateAsyncScope();
            var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
            var actor = ProgramManagementServices.Actor(User);
            var provider = await bus.SendAsync(new ReviseProvider(Tenant, ProviderId, 1, MaterialProvider("Revised provider")), actor);
            Assert.True(provider.IsSuccess, provider.Error?.Message);
            var report = await bus.SendAsync(new RecordAssuranceReport(Tenant, ProviderId, Report()), actor);
            Assert.True(report.IsSuccess, report.Error?.Message);
            ReportId = report.Value.ReportId;
            var revised = await bus.SendAsync(new ReviseAssuranceReport(Tenant, ProviderId, ReportId, 1, Report() with { Opinion = "qualified" }), actor);
            Assert.True(revised.IsSuccess, revised.Error?.Message);
        }
        public Task<ProviderAssuranceRegister> ReadAsync() => ProgramManagementServices.HydrateAsync(_provider, new ProviderAssuranceRegister(Tenant));
        public async Task<Result<ProviderReviewRegistration>> ReviewAsync(string transport, string? refusal = null, RequestMetadata? metadata = null)
        {
            var content = refusal switch
            {
                "report" => Review(Uuid.CreateVersion4()),
                "artifact" => Review() with { Evidence = Citation(artifactId: Uuid.CreateVersion4()) },
                "current_report" => Review(ReportId),
                _ => Review()
            };
            await using var scope = _provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
                new RecordProviderReview(Tenant, refusal == "provider" ? Uuid.CreateVersion4() : ProviderId, content),
                Context(User, transport, metadata), CancellationToken.None);
        }
        public ValueTask DisposeAsync() => _provider.DisposeAsync();
    }
    sealed class Permissions : IPermissionAuthorizer
    {
        public bool Allowed { get; set; } = true;
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission, CancellationToken ct = default) => ValueTask.FromResult(Allowed);
    }
}

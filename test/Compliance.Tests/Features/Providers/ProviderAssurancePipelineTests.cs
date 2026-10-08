using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using static Bdgrz.Compliance.Tests.Features.Providers.AssuranceSamples;

namespace Bdgrz.Compliance.Tests.Features.Providers;

public sealed class ProviderAssurancePipelineTests
{
    [Fact]
    public async Task ShouldRecordReportAndPersonalReviewAndEvaluateCoverageGivenProviderManager()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(RbacPermissions.TenantAccess, RbacPermissions.ProviderInventoryManage);
        var scenario = fixture.Scenario();
        var reportMetadata = RequestMetadata.Create();

        // Act
        var report = (await scenario.GivenMetadata(reportMetadata).When(new RecordAssuranceReport(fixture.TenantId, fixture.ProviderId, Report()))
            .ExpectSuccess()).Value;
        var retry = (await scenario.GivenMetadata(reportMetadata).When(new RecordAssuranceReport(fixture.TenantId, fixture.ProviderId, Report()))
            .ExpectSuccess()).Value;
        var revised = (await scenario.When(new ReviseAssuranceReport(fixture.TenantId, fixture.ProviderId, report.ReportId, 1,
            Report() with { BridgeLetter = new AssuranceBridgeLetter(new DateOnly(2026, 8, 5), new DateOnly(2026, 8, 1), new DateOnly(2026, 12, 31)) }))
            .ExpectSuccess()).Value;
        var stale = await scenario.When(new ReviseAssuranceReport(fixture.TenantId, fixture.ProviderId, report.ReportId, 1, Report()))
            .ExpectFailure(RequestErrorKind.Conflict);
        var review = (await PersonalProviderReviewTransportTests.HttpAsync(fixture.Services, fixture.UserId, new RecordProviderReview(fixture.TenantId, fixture.ProviderId, Review(report.ReportId)))).Value;
        var coverage = (await scenario.When(new GetProviderAssuranceCoverage(fixture.TenantId, fixture.ProviderId, new DateOnly(2026, 10, 2))).ExpectSuccess()).Value;
        var source = await ProgramManagementServices.HydrateAsync(fixture.Services, new ProviderAssuranceRegister(fixture.TenantId));

        // Assert
        Assert.Equal(report, retry);
        Assert.Equal(2, revised.Revision);
        Assert.Contains("Current revision: 2", stale.Error!.Message, StringComparison.Ordinal);
        Assert.Equal("current", coverage.Status);
        Assert.False(coverage.Complete);
        Assert.Equal(["bridged_only"], coverage.Reasons);
        Assert.Equal(review.ReviewId, coverage.LatestReviewId);
        var stored = Assert.Single(source.Reviews(fixture.ProviderId));
        Assert.Equal(RbacIds.Member(fixture.TenantId, fixture.UserId).ToString(), stored.ReviewedBy.Id);
        Assert.Equal(1, stored.ProviderRevision);
        Assert.Equal(2, stored.AssuranceReportRevision);
        Assert.Equal(2, source.Report(report.ReportId)!.Revision);
        Assert.Equal(RbacIds.Member(fixture.TenantId, fixture.UserId).ToString(), source.Report(report.ReportId)!.RecordedBy.Id);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldAllowReadAndDenyWritesGivenAuthorityLevel(bool manager)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(manager ? [RbacPermissions.TenantAccess, RbacPermissions.ProviderInventoryManage] : [RbacPermissions.TenantAccess]);
        var scenario = fixture.Scenario();

        // Act
        var coverage = await scenario.When(new GetProviderAssuranceCoverage(fixture.TenantId, fixture.ProviderId)).ExpectSuccess();
        var write = scenario.When(new RecordAssuranceReport(fixture.TenantId, fixture.ProviderId, Report()));
        var review = scenario.When(new RecordProviderReview(fixture.TenantId, fixture.ProviderId, Review() with { Evidence = Citation() }));

        // Assert
        Assert.Equal("no_review", coverage.Value.Status);
        if (manager)
        {
            await write.ExpectSuccess();
            await PersonalProviderReviewTransportTests.HttpAsync(fixture.Services, fixture.UserId, new RecordProviderReview(fixture.TenantId, fixture.ProviderId, Review() with { Evidence = Citation() }));
        }
        else
        {
            await write.ExpectDenied(RequestErrorKind.Forbidden).ExpectNotHandled();
            await review.ExpectDenied(RequestErrorKind.Forbidden).ExpectNotHandled();
        }
    }

    [Fact]
    public async Task ShouldHideForeignProviderGivenOtherTenantRequests()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(RbacPermissions.TenantAccess, RbacPermissions.ProviderInventoryManage);
        var scenario = fixture.Scenario();
        var otherTenant = Uuid.CreateVersion4();

        // Act
        var record = await scenario.When(new RecordAssuranceReport(otherTenant, fixture.ProviderId, Report())).ExpectFailure(RequestErrorKind.NotFound);
        var review = await PersonalProviderReviewTransportTests.HttpAsync(fixture.Services, fixture.UserId, new RecordProviderReview(otherTenant, fixture.ProviderId, Review() with { Evidence = Citation() }), RequestErrorKind.NotFound);
        var coverage = await scenario.When(new GetProviderAssuranceCoverage(otherTenant, fixture.ProviderId)).ExpectFailure(RequestErrorKind.NotFound);
        var unknown = await scenario.When(new GetProviderAssuranceCoverage(fixture.TenantId, Uuid.CreateVersion4())).ExpectFailure(RequestErrorKind.NotFound);
        var foreignReport = await scenario.When(new RecordAssuranceReport(fixture.TenantId, fixture.ProviderId, Report())).ExpectSuccess();
        var foreignRevision = await scenario.When(new ReviseAssuranceReport(otherTenant, fixture.ProviderId, foreignReport.Value.ReportId, 1, Report()))
            .ExpectFailure(RequestErrorKind.NotFound);

        // Assert
        Assert.Equal(record.Error!.Message, coverage.Error!.Message);
        Assert.Equal(review.Error!.Message, unknown.Error!.Message);
        Assert.Equal(record.Error.Message, foreignRevision.Error!.Message);
        Assert.Empty((await ProgramManagementServices.HydrateAsync(fixture.Services, new ProviderAssuranceRegister(otherTenant))).Reports(fixture.ProviderId));
    }

    [Fact]
    public async Task ShouldValidateGovernedArtifactReferenceGivenCitation()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(RbacPermissions.TenantAccess, RbacPermissions.ProviderInventoryManage);
        var scenario = fixture.Scenario();
        var artifactId = Uuid.CreateVersion4();
        await ProgramManagementServices.SeedAsync(fixture.Services, new EvidenceArtifact(fixture.TenantId, artifactId), artifact =>
            artifact.Register(new EvidenceArtifactContent("SOC 2 report", null, "report", "manual", Now,
                new DateOnly(2026, 6, 30), new DateOnly(2026, 6, 30), "restricted"), new string('a', 64), 1, Author, Now));

        // Act
        var found = await scenario.When(new RecordAssuranceReport(fixture.TenantId, fixture.ProviderId, Report("restricted") with
        {
            Citation = Citation("restricted", artifactId),
        })).ExpectSuccess();
        var missing = await scenario.When(new RecordAssuranceReport(fixture.TenantId, fixture.ProviderId, Report() with
        {
            Citation = Citation("internal", Uuid.CreateVersion4()),
        })).ExpectFailure(RequestErrorKind.NotFound);
        var review = await PersonalProviderReviewTransportTests.HttpAsync(fixture.Services, fixture.UserId, new RecordProviderReview(fixture.TenantId, fixture.ProviderId, Review() with
        {
            Evidence = Citation("internal", Uuid.CreateVersion4()),
        }), RequestErrorKind.NotFound);

        // Assert
        Assert.Equal(1, found.Value.Revision);
        Assert.Contains("artifact", missing.Error!.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("artifact", review.Error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShouldLagListsUntilProjectionCatchesUpGivenRecordedReportAndReview()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(RbacPermissions.TenantAccess, RbacPermissions.ProviderInventoryManage);
        var scenario = fixture.Scenario();
        var report = (await scenario.When(new RecordAssuranceReport(fixture.TenantId, fixture.ProviderId, Report())).ExpectSuccess()).Value;
        await PersonalProviderReviewTransportTests.HttpAsync(fixture.Services, fixture.UserId, new RecordProviderReview(fixture.TenantId, fixture.ProviderId, Review(report.ReportId)));

        // Act
        var lagging = await scenario.When(new ListProviderAssuranceReports(fixture.TenantId, fixture.ProviderId)).ExpectFailure(RequestErrorKind.Conflict);
        var laggingReviews = await scenario.When(new ListProviderReviews(fixture.TenantId, fixture.ProviderId)).ExpectFailure(RequestErrorKind.Conflict);
        await fixture.CatchUpAsync();
        var reports = (await scenario.When(new ListProviderAssuranceReports(fixture.TenantId, fixture.ProviderId)).ExpectSuccess()).Value;
        var reviews = (await scenario.When(new ListProviderReviews(fixture.TenantId, fixture.ProviderId)).ExpectSuccess()).Value;
        var invalidLimit = await scenario.When(new ListProviderReviews(fixture.TenantId, fixture.ProviderId, 0)).ExpectFailure(RequestErrorKind.Validation);

        // Assert
        Assert.True(lagging.Error!.IsTransient);
        Assert.True(laggingReviews.Error!.IsTransient);
        Assert.Equal(report.ReportId, Assert.Single(reports.Items).ReportId);
        Assert.False(reports.Items[0].Redacted);
        Assert.Equal(report.ReportId, Assert.Single(reviews.Items).Content.AssuranceReportId);
        Assert.Equal(RequestErrorKind.Validation, invalidLimit.Error!.Kind);
    }

    [Fact]
    public async Task ShouldNotLeakAcrossTenantsGivenTwoTenantsWithSameProviderIdentity()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(RbacPermissions.TenantAccess, RbacPermissions.ProviderInventoryManage);
        var scenario = fixture.Scenario();
        var otherTenant = Uuid.CreateVersion4();
        await ProgramManagementServices.SeedAsync(fixture.Services, new ProviderRegister(otherTenant), register =>
            register.Record(fixture.ProviderId, Uuid.CreateVersion4(), MaterialProvider("Other tenant provider"), Author, Now));
        var report = (await scenario.When(new RecordAssuranceReport(fixture.TenantId, fixture.ProviderId, Report())).ExpectSuccess()).Value;
        await PersonalProviderReviewTransportTests.HttpAsync(fixture.Services, fixture.UserId, new RecordProviderReview(fixture.TenantId, fixture.ProviderId, Review(report.ReportId)));
        await fixture.CatchUpAsync();

        // Act
        var ownReports = (await scenario.When(new ListProviderAssuranceReports(fixture.TenantId, fixture.ProviderId)).ExpectSuccess()).Value;
        var otherReports = (await scenario.When(new ListProviderAssuranceReports(otherTenant, fixture.ProviderId)).ExpectSuccess()).Value;
        var otherReviews = (await scenario.When(new ListProviderReviews(otherTenant, fixture.ProviderId)).ExpectSuccess()).Value;
        var otherCoverage = (await scenario.When(new GetProviderAssuranceCoverage(otherTenant, fixture.ProviderId)).ExpectSuccess()).Value;
        var foreignProvider = await scenario.When(new ListProviderAssuranceReports(otherTenant, Uuid.CreateVersion4())).ExpectFailure(RequestErrorKind.NotFound);

        // Assert
        Assert.Single(ownReports.Items);
        Assert.Empty(otherReports.Items);
        Assert.Empty(otherReviews.Items);
        Assert.Equal("no_review", otherCoverage.Status);
        Assert.Empty(otherCoverage.Reports);
        Assert.Equal(RequestErrorKind.NotFound, foreignProvider.Error!.Kind);
    }

    sealed class Fixture : IAsyncDisposable
    {
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProviderId { get; } = Uuid.CreateVersion4();
        public ServiceProvider Services { get; private set; } = null!;

        public static async Task<Fixture> CreateAsync(params string[] permissions)
        {
            var kv = new InMemoryKvClient();
            var fixture = new Fixture
            {
                Services = ProgramManagementServices.Build(new Grants(permissions), portia => portia.AddRequestAuthorizer<ProviderAuthorizer>()
                    .AddRequestHandler<RecordAssuranceReportHandler>().AddRequestHandler<ReviseAssuranceReportHandler>()
                    .AddRequestHandler<RecordProviderReviewHandler>().AddRequestHandler<GetProviderAssuranceCoverageHandler>()
                    .AddRequestHandler<ListProviderAssuranceReportsHandler>().AddRequestHandler<ListProviderReviewsHandler>(), services =>
                {
                    var store = new InMemoryEventStore();
                    services.AddSingleton<IEventStore>(store);
                    services.AddSingleton<IDomainEventReader>(store);
                    services.AddSingleton<IKvClient>(kv);
                    services.AddScoped<AssuranceReferences>();
                    services.AddScoped<AssuranceDisclosure>();
                    services.AddScoped<FitzAssuranceDirectory>();
                    services.AddScoped<IAssuranceReader>(provider => provider.GetRequiredService<FitzAssuranceDirectory>());
                    services.AddScoped<AssuranceReadConsistency>();
                }),
            };
            await ProgramManagementServices.SeedAsync(fixture.Services, new ProviderRegister(fixture.TenantId), register =>
                register.Record(fixture.ProviderId, Uuid.CreateVersion4(), MaterialProvider(), Author, Now));
            return fixture;
        }

        public RequestScenario Scenario() => RequestScenario.For(Services).GivenActor(ProgramManagementServices.Actor(UserId));

        public async Task CatchUpAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var directory = scope.ServiceProvider.GetRequiredService<FitzAssuranceDirectory>();
            var events = scope.ServiceProvider.GetRequiredService<IDomainEventReader>();
            foreach (var tenant in new[] { TenantId })
            {
                var checkpoint = await directory.LoadCheckpointAsync(tenant);
                var pattern = EventStreamPattern.ForPattern(tenant.ToString(), ProviderAssuranceRegister.Area);
                await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
                    new CheckpointIdentity(FitzAssuranceDirectory.ProjectorName, pattern), checkpoint));
                var cursor = checkpoint.Cursor;
                await foreach (var record in events.ReadAsync(pattern, checkpoint.Cursor, CancellationToken.None))
                {
                    await directory.ApplyAsync(record.Event);
                    cursor = record.NextCursor;
                }
                await batch.CommitAsync(new ProjectionCheckpoint(cursor));
            }
        }

        public async ValueTask DisposeAsync() => await Services.DisposeAsync();
    }

    sealed class Grants(string[] allowed) : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(allowed.Contains(permission, StringComparer.Ordinal));
    }
}

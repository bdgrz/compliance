using System.Security.Claims;
using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using static Bdgrz.Compliance.Tests.Features.Providers.AssuranceSamples;

namespace Bdgrz.Compliance.Tests.Features.Providers;

public sealed class ProviderAssuranceDisclosureTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldWithholdRestrictedContentGivenReaderWithoutProviderAuthoringAuthority(bool manager)
    {
        // Arrange
        var fixture = await Fixture.CreateAsync();
        var userId = Uuid.CreateVersion4();
        var actor = ProgramManagementServices.Actor(userId);
        var handler = fixture.ReportsHandler(manager);
        var reviews = fixture.ReviewsHandler(manager);

        // Act
        var page = await handler.HandleAsync(new RequestContext<ListProviderAssuranceReports>(
            new ListProviderAssuranceReports(fixture.TenantId, fixture.ProviderId), actor), CancellationToken.None);
        var reviewPage = await reviews.HandleAsync(new RequestContext<ListProviderReviews>(
            new ListProviderReviews(fixture.TenantId, fixture.ProviderId), actor), CancellationToken.None);
        var anonymous = await handler.HandleAsync(new RequestContext<ListProviderAssuranceReports>(
            new ListProviderAssuranceReports(fixture.TenantId, fixture.ProviderId), new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        Assert.True(page.IsSuccess);
        var byIssuer = page.Value.Items.ToDictionary(static item => item.Content.Issuer);
        Assert.Equal(3, byIssuer.Count);
        Assert.Equal(!manager, byIssuer["Restricted LLP"].Redacted);
        Assert.Equal(!manager, byIssuer["Unclassified LLP"].Redacted);
        Assert.False(byIssuer["Internal LLP"].Redacted);
        Assert.Equal(manager ? 1 : 0, byIssuer["Restricted LLP"].Content.Exceptions!.Count);
        Assert.Equal(manager, byIssuer["Restricted LLP"].Content.Citation is not null);
        Assert.Equal(1, byIssuer["Restricted LLP"].ExceptionCount);
        Assert.Equal("unqualified", byIssuer["Restricted LLP"].Content.Opinion);
        Assert.NotNull(byIssuer["Internal LLP"].Content.Citation);
        Assert.Single(byIssuer["Internal LLP"].Content.Exceptions!);
        var byRationale = reviewPage.Value.Items.ToDictionary(static item => item.Content.Rationale);
        Assert.Equal("acceptable", byRationale["restricted evidence"].Content.Conclusion);
        Assert.Equal(manager, byRationale["restricted evidence"].Content.Evidence is not null);
        Assert.Equal(!manager, byRationale["restricted evidence"].Redacted);
        Assert.NotNull(byRationale["internal evidence"].Content.Evidence);
        Assert.False(byRationale["internal evidence"].Redacted);
        Assert.Equal(2, anonymous.Value.Items.Count(static item => item.Redacted));
    }

    sealed class Fixture
    {
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProviderId { get; } = Uuid.CreateVersion4();
        FitzAssuranceDirectory Directory { get; } = new(new InMemoryKvClient());
        ProviderRegister Source { get; set; } = null!;

        public static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture();
            fixture.Source = new ProviderRegister(fixture.TenantId);
            Assert.True(fixture.Source.Record(fixture.ProviderId, Uuid.CreateVersion4(), MaterialProvider(), Author, Now).IsSuccess);
            var exception = new[] { new AssuranceExceptionNote("CC6.1", "Exception text") };
            await using var batch = await fixture.Directory.BeginAsync(new ProjectionBatchContext(new CheckpointIdentity(
                FitzAssuranceDirectory.ProjectorName, EventStreamPattern.ForPattern(fixture.TenantId.ToString(), ProviderAssuranceRegister.Area)),
                ProjectionCheckpoint.Start));
            await fixture.Directory.ApplyAsync(new AssuranceReportRecorded(fixture.TenantId, Uuid.CreateVersion4(), fixture.ProviderId, Uuid.CreateVersion4(),
                Report("restricted") with { Issuer = "Restricted LLP", Exceptions = exception }, 1, Author, Now));
            await fixture.Directory.ApplyAsync(new AssuranceReportRecorded(fixture.TenantId, Uuid.CreateVersion4(), fixture.ProviderId, Uuid.CreateVersion4(),
                Report("internal") with { Issuer = "Internal LLP", Exceptions = exception }, 1, Author, Now));
            await fixture.Directory.ApplyAsync(new AssuranceReportRecorded(fixture.TenantId, Uuid.CreateVersion4(), fixture.ProviderId, Uuid.CreateVersion4(),
                Report() with { Issuer = "Unclassified LLP", Citation = null }, 1, Author, Now));
            await fixture.Directory.ApplyAsync(new ProviderReviewRecorded(fixture.TenantId, Uuid.CreateVersion4(), fixture.ProviderId, Uuid.CreateVersion4(),
                Review() with { Rationale = "restricted evidence", Evidence = Citation("restricted") }, 1, "material", null, Author, Now));
            await fixture.Directory.ApplyAsync(new ProviderReviewRecorded(fixture.TenantId, Uuid.CreateVersion4(), fixture.ProviderId, Uuid.CreateVersion4(),
                Review() with { Rationale = "internal evidence", Evidence = Citation("internal") }, 1, "material", null, Author, Now));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
            return fixture;
        }

        public ListProviderAssuranceReportsHandler ReportsHandler(bool manager) => new(Directory, References(), Consistency(), Disclosure(manager));

        public ListProviderReviewsHandler ReviewsHandler(bool manager) => new(Directory, References(), Consistency(), Disclosure(manager));

        AssuranceReferences References() => new(new SourceReader(Source));

        AssuranceReadConsistency Consistency() => new(Directory, new InMemoryEventStore());

        static AssuranceDisclosure Disclosure(bool manager) => new(new PermissionBackedAccessGrantPermissionAuthorizer(
            new RecordingPermissionAuthorizer(manager)));
    }

    sealed class SourceReader(ProviderRegister source) : IAggregateReader
    {
        public ValueTask<T> HydrateAsync<T>(T aggregate, CancellationToken ct = default) where T : Aggregate =>
            ValueTask.FromResult((T)(Aggregate)source);
    }
}

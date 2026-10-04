using Bdgrz.Compliance.Features.Providers;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using static Bdgrz.Compliance.Tests.Features.Providers.AssuranceSamples;

namespace Bdgrz.Compliance.Tests.Features.Providers;

public sealed class FitzAssuranceDirectoryTests
{
    [Fact]
    public async Task ShouldProjectProviderCoverageGapAndClosureGivenTenantScopedReads()
    {
        // Arrange
        var directory = new FitzAssuranceDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var otherTenantId = Uuid.CreateVersion4();
        var providerId = Uuid.CreateVersion4();
        var gapId = Uuid.CreateVersion4();
        var content = new ProviderCoverageGapContent(Uuid.CreateVersion4(), "Payroll service",
            "Availability assertion", new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30),
            "assurance_report", Uuid.CreateVersion4(), 2, "The period is only partly covered.");
        var recorded = new ProviderCoverageGapRecorded(tenantId, gapId, providerId,
            Uuid.CreateVersion4(), 3, content, Author, Now);
        var acceptance = new ProviderCoverageGapRiskAcceptanceLinked(tenantId, gapId,
            providerId, Uuid.CreateVersion4(), 2,
            new ProviderCoverageGapRiskAcceptanceView(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
                Uuid.CreateVersion4(), Now.AddMonths(2), Author, Now.AddHours(1))
            { Revision = 2 });
        var closed = new ProviderCoverageGapClosed(tenantId, gapId, providerId,
            Uuid.CreateVersion4(), 3,
            new ProviderCoverageGapClosureContent("coverage_restored", "assurance_report",
                Uuid.CreateVersion4(), 3, "A newer report covers the period."), Author,
            Now.AddDays(1));

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(recorded);
            await directory.ApplyAsync(acceptance);
            await directory.ApplyAsync(closed);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        await using (var replay = await directory.BeginAsync(new ProjectionBatchContext(
                         Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(recorded);
            await directory.ApplyAsync(acceptance);
            await directory.ApplyAsync(closed);
            await replay.CommitAsync(ProjectionCheckpoint.Start);
        }
        var gap = await directory.GetCoverageGapAsync(tenantId, gapId);
        var listed = await directory.ListCoverageGapsAsync(tenantId, providerId, 10, null);
        var foreign = await directory.GetCoverageGapAsync(otherTenantId, gapId);
        var foreignList = await directory.ListCoverageGapsAsync(otherTenantId, providerId, 10, null);

        // Assert
        Assert.Equal(content, gap!.Content);
        Assert.Equal(3, gap.ProviderRevision);
        Assert.Equal(3, gap.Revision);
        Assert.Equal("closed", gap.Status);
        Assert.Single(gap.RiskAcceptances!);
        Assert.Equal(acceptance.Acceptance, Assert.Single(gap.RiskAcceptances!));
        Assert.Equal("coverage_restored", gap.Closure!.Content.Resolution);
        Assert.Single(listed.Items);
        Assert.Null(foreign);
        Assert.Empty(foreignList.Items);
        await using var changed = await directory.BeginAsync(new ProjectionBatchContext(
            Identity(tenantId), ProjectionCheckpoint.Start));
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await directory.ApplyAsync(acceptance with
            {
                Acceptance = acceptance.Acceptance with
                {
                    ExpiresAt = acceptance.Acceptance.ExpiresAt.AddDays(1),
                },
            }));
    }

    [Fact]
    public async Task ShouldExhaustScopedPagesGivenTwoTenantsAndRetainedReportsAndReviews()
    {
        // Arrange
        var directory = new FitzAssuranceDirectory(new InMemoryKvClient());
        Uuid[] tenants = [Uuid.CreateVersion4(), Uuid.CreateVersion4()];
        Uuid[] providers = [Uuid.CreateVersion4(), Uuid.CreateVersion4()];
        var reports = new Uuid[2][];
        foreach (var index in Enumerable.Range(0, 2))
        {
            reports[index] = [Uuid.CreateVersion4(), Uuid.CreateVersion4()];
            await using var batch = await directory.BeginAsync(new ProjectionBatchContext(Identity(tenants[index]), ProjectionCheckpoint.Start));
            foreach (var reportId in reports[index])
            {
                await directory.ApplyAsync(new AssuranceReportRecorded(tenants[index], reportId, providers[index], Uuid.CreateVersion4(), Report(), 1, Author, Now));
                await directory.ApplyAsync(new AssuranceReportRevised(tenants[index], reportId, providers[index], Uuid.CreateVersion4(), 2,
                    Report() with { Issuer = "Revised issuer" }, 1, Author, Now.AddMinutes(1)));
                await directory.ApplyAsync(new ProviderReviewRecorded(tenants[index], Uuid.CreateVersion4(), providers[index], Uuid.CreateVersion4(),
                    Review(reportId), 1, "material", 2, Author, Now));
            }
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var seen = new List<AssuranceReportView>();
        var reviews = new List<ProviderReviewView>();
        for (var index = 0; index < 2; index++)
        {
            string? cursor = null;
            do
            {
                var page = await directory.ListReportsAsync(tenants[index], providers[index], 1, cursor);
                seen.AddRange(page.Items);
                cursor = page.NextCursor;
            } while (cursor is not null);
            do
            {
                var page = await directory.ListReviewsAsync(tenants[index], providers[index], 1, cursor);
                reviews.AddRange(page.Items);
                cursor = page.NextCursor;
            } while (cursor is not null);
        }
        var firstReports = await directory.ListReportsAsync(tenants[0], providers[0], 1, null);
        var firstReviews = await directory.ListReviewsAsync(tenants[0], providers[0], 1, null);
        var foreignTenant = await directory.ListReportsAsync(tenants[1], providers[0], 10, null);
        var foreignProvider = await directory.ListReviewsAsync(tenants[0], providers[1], 10, null);

        // Assert
        Assert.Equal(reports.SelectMany(static ids => ids).Order(), seen.Select(static view => view.ReportId).Order());
        Assert.All(seen, view => { Assert.Equal(2, view.Revision); Assert.Equal("Revised issuer", view.Content.Issuer); });
        Assert.Equal(4, reviews.Count);
        Assert.Empty(foreignTenant.Items);
        Assert.Empty(foreignProvider.Items);
        await Assert.ThrowsAsync<KvDirectoryQueryException>(async () => await directory.ListReportsAsync(tenants[1], providers[1], 1, firstReports.NextCursor));
        await Assert.ThrowsAsync<KvDirectoryQueryException>(async () => await directory.ListReviewsAsync(tenants[1], providers[1], 1, firstReviews.NextCursor));
        await Assert.ThrowsAsync<KvDirectoryQueryException>(async () => await directory.ListReportsAsync(tenants[0], providers[1], 1, firstReports.NextCursor));
    }

    [Fact]
    public async Task ShouldRetainStateAndRejectChangedReplayGivenCommittedEvents()
    {
        // Arrange
        var directory = new FitzAssuranceDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var providerId = Uuid.CreateVersion4();
        var reportId = Uuid.CreateVersion4();
        var first = new AssuranceReportRecorded(tenantId, reportId, providerId, Uuid.CreateVersion4(), Report(), 1, Author, Now);
        var second = new AssuranceReportRevised(tenantId, reportId, providerId, Uuid.CreateVersion4(), 2,
            Report() with { Issuer = "Revised" }, 1, Author, Now.AddMinutes(1));
        var review = new ProviderReviewRecorded(tenantId, Uuid.CreateVersion4(), providerId, Uuid.CreateVersion4(), Review(reportId), 1, "material", 2, Author, Now);
        await using (var initial = await directory.BeginAsync(new ProjectionBatchContext(Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(first);
            await directory.ApplyAsync(second);
            await directory.ApplyAsync(review);
            await initial.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        await using (var replay = await directory.BeginAsync(new ProjectionBatchContext(Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(first);
            await directory.ApplyAsync(second);
            await directory.ApplyAsync(review);
            await replay.CommitAsync(ProjectionCheckpoint.Start);
        }
        await using (var changed = await directory.BeginAsync(new ProjectionBatchContext(Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await directory.ApplyAsync(
                second with { Content = second.Content with { Issuer = "Replacement" } }));
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await directory.ApplyAsync(
                review with { Content = review.Content with { Rationale = "Replacement" } }));
        }

        // Assert
        var reports = await directory.ListReportsAsync(tenantId, providerId, 10, null);
        var current = Assert.Single(reports.Items);
        Assert.Equal(2, current.Revision);
        Assert.Equal("Revised", current.Content.Issuer);
        Assert.Single((await directory.ListReviewsAsync(tenantId, providerId, 10, null)).Items);
    }

    [Theory]
    [InlineData("foreign_tenant")]
    [InlineData("empty_tenant")]
    [InlineData("empty_provider")]
    [InlineData("revision_gap")]
    [InlineData("revision_without_predecessor")]
    public async Task ShouldRejectBeforeCommitGivenEventOutsideItsBatchOrOrder(string invalid)
    {
        // Arrange
        var directory = new FitzAssuranceDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        DomainEvent ev = invalid switch
        {
            "revision_gap" => new AssuranceReportRevised(tenantId, Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(), 3, Report(), 1, Author, Now),
            "revision_without_predecessor" => new AssuranceReportRevised(tenantId, Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(), 2, Report(), 1, Author, Now),
            _ => new AssuranceReportRecorded(invalid == "foreign_tenant" ? Uuid.CreateVersion4() : invalid == "empty_tenant" ? Uuid.Empty : tenantId,
                Uuid.CreateVersion4(), invalid == "empty_provider" ? Uuid.Empty : Uuid.CreateVersion4(), Uuid.CreateVersion4(), Report(), 1, Author, Now),
        };

        // Act
        await using var batch = await ((IAssuranceProjection)directory).BeginAsync(
            new ProjectionBatchContext(Identity(tenantId), ProjectionCheckpoint.Start));

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await directory.ApplyAsync(ev));
    }

    [Fact]
    public async Task ShouldRecoverWithoutPartialRowsGivenAbortedProjectionBatch()
    {
        // Arrange
        var directory = new FitzAssuranceDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var providerId = Uuid.CreateVersion4();
        var reportId = Uuid.CreateVersion4();
        var first = new AssuranceReportRecorded(tenantId, reportId, providerId, Uuid.CreateVersion4(), Report(), 1, Author, Now);

        // Act
        await using (var interrupted = await directory.BeginAsync(new ProjectionBatchContext(Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(first);
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await directory.ApplyAsync(
                new AssuranceReportRevised(tenantId, reportId, providerId, Uuid.CreateVersion4(), 3, Report(), 1, Author, Now)));
        }
        var before = await directory.ListReportsAsync(tenantId, providerId, 10, null);
        await using (var retry = await directory.BeginAsync(new ProjectionBatchContext(Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(first);
            await retry.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Assert
        Assert.Empty(before.Items);
        Assert.Single((await directory.ListReportsAsync(tenantId, providerId, 10, null)).Items);
    }

    static CheckpointIdentity Identity(Uuid tenantId) => new(FitzAssuranceDirectory.ProjectorName,
        EventStreamPattern.ForPattern(tenantId.ToString(), ProviderAssuranceRegister.Area));
}

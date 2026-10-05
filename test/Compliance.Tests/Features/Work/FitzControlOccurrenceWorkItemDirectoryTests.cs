using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Features.Operations;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class FitzControlOccurrenceWorkItemDirectoryTests
{
    [Fact]
    public async Task ShouldReturnNoOccurrenceWorkGivenInitialPendingPlan()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.ProposeAsync(0);
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var directory = new FitzControlOccurrenceWorkItemDirectory(new InMemoryKvClient(),
            sourceScope.ServiceProvider.GetRequiredService<IAggregateReader>());
        await CatchUpAsync(fixture, directory);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act
        var work = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId, today,
            today.AddDays(30), null, CancellationToken.None);

        // Assert
        Assert.True(work.IsSuccess);
        Assert.Empty(work.Value);
    }

    [Fact]
    public async Task ShouldMatchLedgerAfterReassigningOpenEventDrivenOccurrenceGivenReplacementPlan()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var cadence = new ControlCadence("ad_hoc", DueWithinDays: 10);
        await fixture.PlanAsync(cadence: cadence);
        var opened = await fixture.AsAsync(fixture.OwnerUserId, new OpenControlOccurrence(
            fixture.TenantId, fixture.ProgramId, fixture.ControlId, "Quarterly access review",
            fixture.Today));
        var replacementOwner = new OperatingHolder(OperatingAuthority.MemberHolder,
            fixture.BackupMemberId);
        await fixture.PlanAsync(owner: replacementOwner, cadence: cadence,
            effectiveFrom: fixture.Today);
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var sourceReader = sourceScope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var horizon = today.AddDays(30);
        var ledgerWork = await WorkSource.LoadAsync(sourceReader, fixture.TenantId,
            fixture.ProgramId, today, horizon, now, null, fixture.Boundaries,
            new HashSet<string>(StringComparer.Ordinal), CancellationToken.None);
        var directory = new FitzControlOccurrenceWorkItemDirectory(new InMemoryKvClient(),
            sourceReader);
        await CatchUpAsync(fixture, directory);

        // Act
        var projectedWork = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            today, horizon, null, CancellationToken.None);

        // Assert
        Assert.True(ledgerWork.IsSuccess);
        Assert.True(projectedWork.IsSuccess);
        AssertEquivalentCandidates(ledgerWork.Value.Where(IsOccurrenceWork).ToArray(),
            projectedWork.Value);
        var candidate = Assert.Single(projectedWork.Value,
            item => item.Kind == WorkSource.ControlOccurrence && item.SourceId == opened.OccurrenceId);
        Assert.Equal(replacementOwner, candidate.Responsible);
    }

    [Fact]
    public async Task ShouldMatchLedgerOccurrenceCandidatesGivenApprovedPlanAndHorizon()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync();
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var horizon = today.AddDays(30);
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var sourceReader = sourceScope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var ledgerWork = await WorkSource.LoadAsync(sourceReader, fixture.TenantId,
            fixture.ProgramId, today, horizon, now, null, fixture.Boundaries,
            new HashSet<string>(StringComparer.Ordinal), CancellationToken.None);
        var directory = new FitzControlOccurrenceWorkItemDirectory(new InMemoryKvClient(),
            sourceReader);
        await CatchUpAsync(fixture, directory);

        // Act
        var projectedWork = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            today, horizon, null, CancellationToken.None);

        // Assert
        Assert.True(ledgerWork.IsSuccess);
        Assert.True(projectedWork.IsSuccess);
        AssertEquivalentCandidates(ledgerWork.Value.Where(IsOccurrenceWork).ToArray(),
            projectedWork.Value);
    }

    [Fact]
    public async Task ShouldMatchLedgerAcrossRecurringPlanHistoryGivenReplacementCadence()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync();
        var replacementCadence = new ControlCadence("recurring", "quarterly",
            fixture.Today.AddMonths(1), 8);
        await fixture.PlanAsync(cadence: replacementCadence, effectiveFrom: fixture.Today);
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var horizon = today.AddDays(120);
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var sourceReader = sourceScope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var ledgerWork = await WorkSource.LoadAsync(sourceReader, fixture.TenantId,
            fixture.ProgramId, today, horizon, now, null, fixture.Boundaries,
            new HashSet<string>(StringComparer.Ordinal), CancellationToken.None);
        var directory = new FitzControlOccurrenceWorkItemDirectory(new InMemoryKvClient(),
            sourceReader);
        await CatchUpAsync(fixture, directory);

        // Act
        var projectedWork = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            today, horizon, null, CancellationToken.None);

        // Assert
        Assert.True(ledgerWork.IsSuccess);
        Assert.True(projectedWork.IsSuccess);
        AssertEquivalentCandidates(ledgerWork.Value.Where(IsOccurrenceWork).ToArray(),
            projectedWork.Value);
        Assert.Contains(projectedWork.Value, item => item.Kind == WorkSource.ControlOccurrence &&
            item.DueOn > fixture.Today.AddDays(20));
    }

    [Fact]
    public async Task ShouldProjectPendingReviewAndReturnedOccurrenceGivenAttestationAndDecision()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var plan = await fixture.PlanAsync();
        var occurrence = (await fixture.OccurrencesAsync())
            .First(item => item.PeriodStart <= fixture.Today);
        var attested = await fixture.AsAsync(fixture.OwnerUserId, fixture.Attest(occurrence));
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var sourceReader = sourceScope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var directory = new FitzControlOccurrenceWorkItemDirectory(new InMemoryKvClient(),
            sourceReader);
        await CatchUpAsync(fixture, directory);
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var horizon = today.AddDays(30);

        // Act
        var submitted = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            today, horizon, null, CancellationToken.None);
        await fixture.AsAsync(fixture.ReviewerUserId, fixture.Review(attested, "returned"));
        await CatchUpAsync(fixture, directory);
        var returned = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            today, horizon, null, CancellationToken.None);

        // Assert
        Assert.True(submitted.IsSuccess);
        var review = Assert.Single(submitted.Value,
            item => item.Kind == WorkSource.OccurrenceReview);
        Assert.Equal(occurrence.OccurrenceId, review.SourceId);
        Assert.Equal("review", review.NextAction);
        Assert.Equal($"/api/v1/tenants/{fixture.TenantId}/programs/{fixture.ProgramId}/" +
                     $"controls/{fixture.ControlId}/occurrences/{occurrence.OccurrenceId}/reviews",
            review.ActionPath);
        Assert.Equal(new OperatingHolder(OperatingAuthority.MemberHolder,
            plan.ReviewerMemberId), review.Responsible);
        Assert.Contains(fixture.OwnerMemberId, review.Excluded);
        Assert.True(returned.IsSuccess);
        Assert.DoesNotContain(returned.Value,
            item => item.Kind == WorkSource.OccurrenceReview &&
                    item.SourceId == occurrence.OccurrenceId);
        var returnedOccurrence = Assert.Single(returned.Value,
            item => item.Kind == WorkSource.ControlOccurrence &&
                    item.SourceId == occurrence.OccurrenceId);
        Assert.Equal("attest", returnedOccurrence.NextAction);
        Assert.Equal(occurrence.DueOn, returnedOccurrence.DueOn);
    }

    [Fact]
    public async Task ShouldKeepReviewWorkGivenDeferredDecision()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var plan = await fixture.PlanAsync();
        var occurrence = (await fixture.OccurrencesAsync())
            .First(item => item.PeriodStart <= fixture.Today);
        var attested = await fixture.AsAsync(fixture.OwnerUserId, fixture.Attest(occurrence));
        var deferred = await fixture.AsAsync(fixture.ReviewerUserId,
            fixture.Review(attested, "deferred"));
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var sourceReader = sourceScope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var directory = new FitzControlOccurrenceWorkItemDirectory(new InMemoryKvClient(),
            sourceReader);
        await CatchUpAsync(fixture, directory);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act
        var work = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId, today,
            today.AddDays(30), null, CancellationToken.None);

        // Assert
        Assert.Equal("deferred", deferred.State);
        Assert.True(work.IsSuccess);
        var review = Assert.Single(work.Value, item =>
            item.Kind == WorkSource.OccurrenceReview && item.SourceId == occurrence.OccurrenceId);
        Assert.Equal(new OperatingHolder(OperatingAuthority.MemberHolder,
            plan.ReviewerMemberId), review.Responsible);
        Assert.Contains(fixture.OwnerMemberId, review.Excluded);
    }

    [Fact]
    public async Task ShouldProjectLatestCorrectionAsPendingReviewGivenReturnedAttestation()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var plan = await fixture.PlanAsync(backup: new OperatingHolder(
            OperatingAuthority.MemberHolder, fixture.BackupMemberId));
        var occurrence = (await fixture.OccurrencesAsync("missed"))[0];
        var submitted = await fixture.AsAsync(fixture.OwnerUserId, fixture.Attest(occurrence));
        var returned = await fixture.AsAsync(fixture.ReviewerUserId,
            fixture.Review(submitted, "returned"));
        var corrected = await fixture.AsAsync(fixture.BackupUserId,
            new CorrectControlAttestation(fixture.TenantId, fixture.ProgramId, fixture.ControlId,
                returned.OccurrenceId, returned.Revision, "complete",
                DateTimeOffset.UtcNow.AddMinutes(-1), null, null, "Correction recorded.", null,
                OperationsFixture.FullSupport, "Included the missing support."));
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var sourceReader = sourceScope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var directory = new FitzControlOccurrenceWorkItemDirectory(new InMemoryKvClient(),
            sourceReader);
        await CatchUpAsync(fixture, directory);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act
        var work = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId, today,
            today.AddDays(30), null, CancellationToken.None);

        // Assert
        Assert.Equal(2, corrected.Attestations.Count);
        Assert.Equal(corrected.Attestations[0].AttestationId,
            corrected.Attestations[1].SupersedesAttestationId);
        Assert.True(work.IsSuccess);
        var review = Assert.Single(work.Value, item =>
            item.Kind == WorkSource.OccurrenceReview && item.SourceId == occurrence.OccurrenceId);
        Assert.Equal(plan.ReviewerMemberId, review.Responsible.Id);
        Assert.Equal(corrected.Attestations[^1].RecordedAt, review.CreatedAt);
        Assert.Contains(fixture.BackupMemberId, review.Excluded);
        Assert.DoesNotContain(fixture.OwnerMemberId, review.Excluded);
    }

    [Fact]
    public async Task ShouldIsolateProjectedOccurrencesToTheirTenantAndProgramGivenPlan()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync();
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var directory = new FitzControlOccurrenceWorkItemDirectory(new InMemoryKvClient(),
            sourceScope.ServiceProvider.GetRequiredService<IAggregateReader>());
        await CatchUpAsync(fixture, directory);
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var horizon = today.AddDays(30);

        // Act
        var otherProgram = await directory.LoadProgramAsync(fixture.TenantId,
            Uuid.CreateVersion4(), today, horizon, null, CancellationToken.None);
        var otherTenant = await directory.LoadProgramAsync(Uuid.CreateVersion4(), fixture.ProgramId,
            today, horizon, null, CancellationToken.None);

        // Assert
        Assert.True(otherProgram.IsSuccess);
        Assert.Empty(otherProgram.Value);
        Assert.True(otherTenant.IsSuccess);
        Assert.Empty(otherTenant.Value);
    }

    internal static async Task CatchUpAsync(OperationsFixture fixture,
        FitzControlOccurrenceWorkItemDirectory directory)
    {
        var pattern = directory.SourcePattern(fixture.TenantId);
        var checkpoint = await directory.LoadCheckpointAsync(fixture.TenantId);
        var identity = new CheckpointIdentity(FitzControlOccurrenceWorkItemDirectory.ProjectorName,
            pattern);
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            checkpoint));
        var cursor = checkpoint.Cursor;
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        await foreach (var record in events.ReadAsync(pattern, cursor, CancellationToken.None))
        {
            if (record.Event is ControlOperatingPlanProposed or ControlOperatingPlanApproved or
                ControlOccurrenceOpened or ControlOccurrenceAttested or ControlOccurrenceReviewed)
                await directory.ApplyAsync(record.Event);
            cursor = record.NextCursor;
        }
        await batch.CommitAsync(new ProjectionCheckpoint(cursor));
    }

    static bool IsOccurrenceWork(WorkCandidate candidate) =>
        candidate.Kind is WorkSource.ControlOccurrence or WorkSource.OccurrenceReview;

    static void AssertEquivalentCandidates(WorkCandidate[] expected,
        IReadOnlyList<WorkCandidate> actual)
    {
        Assert.Equal(expected.Length, actual.Count);
        var actualById = actual.ToDictionary(static candidate => candidate.WorkItemId);
        foreach (var candidate in expected)
        {
            Assert.True(actualById.TryGetValue(candidate.WorkItemId, out var projected));
            Assert.Equal(candidate.Kind, projected!.Kind);
            Assert.Equal(candidate.SourceId, projected.SourceId);
            Assert.Equal(candidate.ControlId, projected.ControlId);
            Assert.Equal(candidate.Summary, projected.Summary);
            Assert.Equal(candidate.Reason, projected.Reason);
            Assert.Equal(candidate.DueOn, projected.DueOn);
            Assert.Equal(candidate.NextAction, projected.NextAction);
            Assert.Equal(candidate.ActionPath, projected.ActionPath);
            Assert.Equal(candidate.Responsible, projected.Responsible);
            Assert.Equal(candidate.Backup, projected.Backup);
            Assert.Equal(candidate.Excluded.OrderBy(static member => member.ToString()),
                projected.Excluded.OrderBy(static member => member.ToString()));
            Assert.Equal(candidate.CreatedAt, projected.CreatedAt);
        }
    }
}

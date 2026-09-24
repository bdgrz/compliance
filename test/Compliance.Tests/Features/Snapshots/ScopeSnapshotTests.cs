using System.Security.Claims;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Snapshots;

public sealed class ScopeSnapshotTests
{
    [Fact]
    public void ShouldRetainOriginalGivenAttributableAmendment()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var initialId = Uuid.CreateVersion4();
        var amendedId = Uuid.CreateVersion4();
        var siblingId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var manifest = Manifest(tenantId, programId);
        var (json, digest) = SnapshotContentIdentity.Manifest(manifest);
        var revisedManifest = manifest with { ProgramRevision = 2 };
        var (revisedJson, revisedDigest) = SnapshotContentIdentity.Manifest(revisedManifest);
        var initial = new ImmutableSnapshot(tenantId, initialId);
        var amendment = new ImmutableSnapshot(tenantId, amendedId);
        var sibling = new ImmutableSnapshot(tenantId, siblingId);
        var now = DateTimeOffset.UtcNow;

        // Act
        var first = initial.Freeze(initialId, null, programId, manifest, json, digest,
            null, actorId, "Lead", now);
        var repeated = initial.Freeze(initialId, null, programId, manifest, json, digest,
            null, actorId, "Lead", now);
        var unsupportedRetry = initial.Freeze(initialId, null, programId,
            manifest with { FormatVersion = 2 }, json, digest,
            null, actorId, "Lead", now);
        var forged = amendment.Freeze(initialId, initialId, programId,
            revisedManifest, json, "different-content-digest",
            "Corrected scope", actorId, "Lead", now.AddMinutes(1));
        var unsupported = amendment.Freeze(initialId, initialId, programId,
            manifest with { FormatVersion = 2 }, json, digest,
            "Corrected scope", actorId, "Lead", now.AddMinutes(1));
        var revised = amendment.Freeze(initialId, initialId, programId,
            revisedManifest, revisedJson, revisedDigest,
            "  Corrected scope  ", actorId, "Lead", now.AddMinutes(1));
        var retried = amendment.Freeze(initialId, initialId, programId,
            revisedManifest, revisedJson, revisedDigest,
            "Corrected scope", actorId, "Lead", now.AddMinutes(1));
        var changed = amendment.Freeze(initialId, initialId, programId,
            manifest, json, digest, "Corrected scope", actorId, "Lead", now);
        var branched = sibling.Freeze(initialId, initialId, programId,
            revisedManifest, revisedJson, revisedDigest,
            "Alternative correction", actorId, "Lead", now.AddMinutes(2));

        // Assert
        Assert.True(first.IsSuccess);
        Assert.True(repeated.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation,
            Assert.IsType<RequestError>(unsupportedRetry.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(forged.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(unsupported.Error).Kind);
        Assert.True(revised.IsSuccess);
        Assert.True(retried.IsSuccess);
        Assert.True(branched.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict,
            Assert.IsType<RequestError>(changed.Error).Kind);
        Assert.True(initial.IsFrozen);
        Assert.Equal(initialId, initial.RootSnapshotId);
        Assert.Equal(initialId, amendment.RootSnapshotId);
        Assert.Equal(initialId, amendment.AmendsSnapshotId);
        Assert.Equal(initialId, sibling.RootSnapshotId);
        Assert.Equal(initialId, sibling.AmendsSnapshotId);
        Assert.Equal(programId, amendment.ProgramId);
    }

    [Fact]
    public void ShouldHashCanonicalSourceGivenEquivalentUnicodeAndDifferentVersions()
    {
        // Arrange
        var programId = Uuid.CreateVersion4();
        var memberId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);
        var plan = new ProgramPlan(null, null, null, null, null, null);
        var first = new ProgramRevisionView(programId, 1, "Caf\u00e9", plan,
            memberId, "Lead", now);
        var equivalent = first with { Name = "Cafe\u0301" };
        var revised = first with { Revision = 2 };

        // Act
        var firstHash = SnapshotContentIdentity.ProgramRevision(first);
        var equivalentHash = SnapshotContentIdentity.ProgramRevision(equivalent);
        var revisedHash = SnapshotContentIdentity.ProgramRevision(revised);

        // Assert
        Assert.Equal(firstHash, equivalentHash);
        Assert.NotEqual(firstHash, revisedHash);
        Assert.Equal(64, firstHash.Length);
    }

    [Fact]
    public void ShouldRejectChangedStoredManifestGivenOriginalDigest()
    {
        // Arrange
        var manifest = Manifest(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var (json, digest) = SnapshotContentIdentity.Manifest(manifest);

        // Act
        var changedBytes = SnapshotContentIdentity.MatchesManifest(manifest,
            json.Replace("program_scope", "other_scope", StringComparison.Ordinal), digest);
        var changedDigest = SnapshotContentIdentity.MatchesManifest(manifest, json,
            new string('f', 64));
        var untouched = SnapshotContentIdentity.MatchesManifest(manifest, json, digest);

        // Assert
        Assert.False(changedBytes);
        Assert.False(changedDigest);
        Assert.True(untouched);
    }

    [Fact]
    public async Task ShouldRegenerateEquivalentManifestGivenRetainedFrozenSource()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var snapshotId = Uuid.CreateVersion4();
        var manifest = Manifest(tenantId, programId);
        var (json, digest) = SnapshotContentIdentity.Manifest(manifest);
        var source = new ImmutableSnapshot(tenantId, snapshotId);
        Assert.True(source.Freeze(snapshotId, null, programId, manifest, json, digest,
            null, Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow).IsSuccess);
        var handler = new RegenerateProgramScopeSnapshotManifestHandler(
            new SnapshotSourceReader(source));

        // Act
        var result = await handler.HandleAsync(
            Context(new RegenerateProgramScopeSnapshotManifest(tenantId, snapshotId)),
            CancellationToken.None);

        // Assert
        var regenerated = Assert.IsType<ProgramScopeSnapshotManifestRegeneration>(result.Value);
        Assert.Equal(tenantId, regenerated.TenantId);
        Assert.Equal(snapshotId, regenerated.SnapshotId);
        Assert.Equal(json, regenerated.CanonicalManifest);
        Assert.Equal(digest, regenerated.ContentSha256);
    }

    [Fact]
    public async Task ShouldRegenerateEighthAmendmentGivenCompleteBoundedLineage()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var rootId = Uuid.CreateVersion4();
        var manifest = Manifest(tenantId, programId);
        var (json, digest) = SnapshotContentIdentity.Manifest(manifest);
        var snapshots = CreateLineage(tenantId, programId, actorId, rootId, manifest, json,
            digest, 8);
        var leaf = snapshots[^1];
        var reader = new SnapshotSourceReader(snapshots.ToArray());
        var handler = new RegenerateProgramScopeSnapshotManifestHandler(reader);

        // Act
        var result = await handler.HandleAsync(
            Context(new RegenerateProgramScopeSnapshotManifest(tenantId, leaf.Id)),
            CancellationToken.None);

        // Assert
        var regenerated = Assert.IsType<ProgramScopeSnapshotManifestRegeneration>(result.Value);
        Assert.Equal(json, regenerated.CanonicalManifest);
        Assert.Equal(digest, regenerated.ContentSha256);
    }

    [Fact]
    public async Task ShouldRejectMalformedOrCyclicSourceGivenManifestRegeneration()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var otherTenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var rootId = Uuid.CreateVersion4();
        var sourceId = Uuid.CreateVersion4();
        var predecessorId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var manifest = Manifest(tenantId, programId);
        var (json, digest) = SnapshotContentIdentity.Manifest(manifest);
        var missingId = Uuid.CreateVersion4();
        var missing = new ImmutableSnapshot(tenantId, missingId);
        var malformedId = Uuid.CreateVersion4();
        var malformed = new ImmutableSnapshot(tenantId, malformedId);
        new AggregateScenario<ImmutableSnapshot>(malformed).Given(DomainEventSeed.Attach(
            new SnapshotFrozen(tenantId, malformedId, malformedId, null, programId,
                "program_scope", manifest, json + " ", digest, null, actorId, "Lead",
                DateTimeOffset.UtcNow), malformedId, 1));
        var crossTenantId = Uuid.CreateVersion4();
        var crossTenant = new ImmutableSnapshot(tenantId, crossTenantId);
        new AggregateScenario<ImmutableSnapshot>(crossTenant).Given(DomainEventSeed.Attach(
            new SnapshotFrozen(otherTenantId, crossTenantId, crossTenantId, null, programId,
                "program_scope", manifest, json, digest, null, actorId, "Lead",
                DateTimeOffset.UtcNow), crossTenantId, 1));
        var cyclicSource = new ImmutableSnapshot(tenantId, sourceId);
        new AggregateScenario<ImmutableSnapshot>(cyclicSource).Given(DomainEventSeed.Attach(
            new SnapshotFrozen(tenantId, sourceId, rootId, predecessorId, programId,
                "program_scope", manifest, json, digest, "Correction", actorId, "Lead",
                DateTimeOffset.UtcNow), sourceId, 1));
        var cyclicPredecessor = new ImmutableSnapshot(tenantId, predecessorId);
        new AggregateScenario<ImmutableSnapshot>(cyclicPredecessor).Given(DomainEventSeed.Attach(
            new SnapshotFrozen(tenantId, predecessorId, rootId, sourceId, programId,
                "program_scope", manifest, json, digest, "Earlier correction", actorId,
                "Lead", DateTimeOffset.UtcNow), predecessorId, 1));
        var reader = new SnapshotSourceReader(missing, malformed, crossTenant, cyclicSource,
            cyclicPredecessor);
        var handler = new RegenerateProgramScopeSnapshotManifestHandler(reader);

        // Act
        var missingResult = await handler.HandleAsync(
            Context(new RegenerateProgramScopeSnapshotManifest(tenantId, missingId)),
            CancellationToken.None);
        var malformedResult = await handler.HandleAsync(
            Context(new RegenerateProgramScopeSnapshotManifest(tenantId, malformedId)),
            CancellationToken.None);
        var crossTenantResult = await handler.HandleAsync(
            Context(new RegenerateProgramScopeSnapshotManifest(tenantId, crossTenantId)),
            CancellationToken.None);
        var cyclicResult = await handler.HandleAsync(
            Context(new RegenerateProgramScopeSnapshotManifest(tenantId, sourceId)),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(missingResult.Error).Kind);
        Assert.All([malformedResult, crossTenantResult, cyclicResult], result =>
        {
            var error = Assert.IsType<RequestError>(result.Error);
            Assert.Equal(RequestErrorKind.Conflict, error.Kind);
            Assert.False(error.IsTransient);
        });
    }

    [Fact]
    public async Task ShouldRejectNinthAmendmentGivenManifestRegeneration()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var rootId = Uuid.CreateVersion4();
        var manifest = Manifest(tenantId, programId);
        var (json, digest) = SnapshotContentIdentity.Manifest(manifest);
        var snapshots = CreateLineage(tenantId, programId, actorId, rootId, manifest, json,
            digest, 9);
        var predecessor = snapshots[^1];
        var reader = new SnapshotSourceReader(snapshots.ToArray());
        var handler = new RegenerateProgramScopeSnapshotManifestHandler(reader);

        // Act
        var result = await handler.HandleAsync(
            Context(new RegenerateProgramScopeSnapshotManifest(tenantId, predecessor.Id)),
            CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.Contains("lineage", error.Message, StringComparison.Ordinal);
        Assert.Equal(9, reader.HydrationCount);
    }

    [Fact]
    public async Task ShouldRejectNinthAmendmentBeforeAppendGivenBoundedLineage()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var boundaryVersionId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var reviewerId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var plan = new ProgramPlan(null, null, null, null, null, null);
        var program = new ComplianceProgram(tenantId, programId);
        Assert.Null(program.Create("SOC 2", plan, actorId, "Lead", now));
        var programRevision = new ProgramRevisionView(programId, 1, "SOC 2", plan,
            actorId, "Lead", now);
        var boundary = new SystemBoundary(tenantId, boundaryId);
        var content = new BoundaryContent("Scope", "readiness", ["security"], []);
        Assert.True(boundary.Create(programId, boundaryVersionId, content, actorId, "Lead", now)
            .IsSuccess);
        var decisionId = Uuid.CreateVersion4();
        Assert.Null(boundary.Review(boundaryVersionId, 1, decisionId, "accept", "Reviewed",
            reviewerId, "Reviewer", now));
        Assert.Null(boundary.Approve(boundaryVersionId, 1, Uuid.CreateVersion4(), decisionId,
            new DateOnly(2027, 1, 1), "Approved", "impact", reviewerId, "Reviewer", now));
        var boundaryVersion = new BoundaryVersionView(tenantId, boundaryId, programId,
            boundaryVersionId, 1, content, "approved", new DateOnly(2027, 1, 1), actorId,
            "Lead", now);
        var manifest = new ProgramScopeManifest(1, tenantId, programId, 1,
            SnapshotContentIdentity.ProgramRevision(programRevision), boundaryId,
            boundaryVersionId, SnapshotContentIdentity.ApprovedBoundaryVersion(boundaryVersion));
        var (json, digest) = SnapshotContentIdentity.Manifest(manifest);
        var snapshots = CreateLineage(tenantId, programId, actorId, Uuid.CreateVersion4(),
            manifest, json, digest, 8);
        var predecessor = snapshots[^1];
        var freezer = new ScopeSnapshotFreezer(new ProgramRevisionReader(programRevision),
            new LaggingBoundaryDirectory(boundaryVersion),
            new SnapshotCreationSourcesReader(program, boundary, snapshots), null!,
            TimeProvider.System);
        var request = new AmendProgramScopeSnapshot(tenantId, predecessor.Id, programId, 1,
            boundaryId, boundaryVersionId, "Correction 9");

        // Act
        var result = await freezer.FreezeAsync(
            Context(request), tenantId, programId,
            1, boundaryId, boundaryVersionId, predecessor.Id, request.Reason,
            CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.Contains("lineage", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldRejectCorruptProjectionAndReportLagGivenFrozenSource()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var snapshotId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var manifest = Manifest(tenantId, programId);
        var (json, digest) = SnapshotContentIdentity.Manifest(manifest);
        var source = new ImmutableSnapshot(tenantId, snapshotId);
        Assert.True(source.Freeze(snapshotId, null, programId, manifest, json, digest,
            null, actorId, "Lead", now).IsSuccess);
        var corrupted = new SnapshotView(tenantId, snapshotId, snapshotId, null,
            programId, "program_scope", 1, manifest, json + " ", digest,
            null, actorId, "Lead", now);

        // Act
        var lag = await new GetSnapshotHandler(new SnapshotDirectoryStub(null),
            new SnapshotSourceReader(source)).HandleAsync(
            Context(new GetSnapshot(tenantId, snapshotId, 1)),
            CancellationToken.None);
        var corrupt = await new GetSnapshotHandler(new SnapshotDirectoryStub(corrupted),
            new SnapshotSourceReader(source)).HandleAsync(
            Context(new GetSnapshot(tenantId, snapshotId)),
            CancellationToken.None);
        var foreign = await new GetSnapshotHandler(new SnapshotDirectoryStub(
                corrupted with { TenantId = Uuid.CreateVersion4() }),
            new SnapshotSourceReader(source)).HandleAsync(
            Context(new GetSnapshot(tenantId, snapshotId)),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(lag.Error).Kind);
        Assert.Contains("projection", lag.Error.Message, StringComparison.Ordinal);
        Assert.Equal(RequestErrorKind.Conflict,
            Assert.IsType<RequestError>(corrupt.Error).Kind);
        Assert.Contains("integrity", corrupt.Error.Message, StringComparison.Ordinal);
        Assert.Equal(RequestErrorKind.NotFound,
            Assert.IsType<RequestError>(foreign.Error).Kind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldReportLagGivenHistoricalApprovalBeforeProjectedVersion(bool projectedDraft)
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var firstVersionId = Uuid.CreateVersion4();
        var secondVersionId = Uuid.CreateVersion4();
        var authorId = Uuid.CreateVersion4();
        var reviewerId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var plan = new ProgramPlan(null, null, null, null, null, null);
        var program = new ComplianceProgram(tenantId, programId);
        Assert.Null(program.Create("SOC 2", plan, authorId, "Lead", now));
        var boundary = new SystemBoundary(tenantId, boundaryId);
        var content = new BoundaryContent("Scope", "readiness", ["security"], []);
        Assert.True(boundary.Create(programId, firstVersionId, content,
            authorId, "Lead", now).IsSuccess);
        var firstDecisionId = Uuid.CreateVersion4();
        Assert.Null(boundary.Review(firstVersionId, 1, firstDecisionId, "accept", "Reviewed",
            reviewerId, "Reviewer", now));
        Assert.Null(boundary.Approve(firstVersionId, 1, Uuid.CreateVersion4(), firstDecisionId,
            new DateOnly(2027, 1, 1), "Approved", "impact-1", reviewerId, "Reviewer", now));
        Assert.Null(boundary.ProposeSuccessor(firstVersionId, secondVersionId, content,
            authorId, "Lead", now));
        var secondDecisionId = Uuid.CreateVersion4();
        Assert.Null(boundary.Review(secondVersionId, 1, secondDecisionId, "accept", "Reviewed",
            reviewerId, "Reviewer", now));
        Assert.Null(boundary.Approve(secondVersionId, 1, Uuid.CreateVersion4(), secondDecisionId,
            new DateOnly(2027, 2, 1), "Approved", "impact-2", reviewerId, "Reviewer", now));
        var projected = projectedDraft
            ? new BoundaryVersionView(tenantId, boundaryId, programId, firstVersionId, 1,
                content, "draft", null, authorId, "Lead", now)
            : null;
        var freezer = new ScopeSnapshotFreezer(
            new ProgramRevisionReader(new ProgramRevisionView(programId, 1, "SOC 2",
                plan, authorId, "Lead", now)),
            new LaggingBoundaryDirectory(projected),
            new SnapshotSourcesReader(program, boundary), null!, TimeProvider.System);
        var request = new FreezeProgramScopeSnapshot(tenantId, programId, 1,
            boundaryId, firstVersionId);

        // Act
        var result = await freezer.FreezeAsync(Context(request), tenantId, programId, 1, boundaryId, firstVersionId,
            null, null, CancellationToken.None);

        // Assert
        Assert.True(boundary.IsVersionApproved(firstVersionId));
        Assert.True(boundary.IsVersionApproved(secondVersionId));
        Assert.Equal(secondVersionId, boundary.LatestApprovedVersionId);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Contains("projection", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldProjectTenantScopedImmutableSnapshotGivenFitzBatch()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var otherTenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var snapshotId = Uuid.CreateVersion4();
        var manifest = Manifest(tenantId, programId);
        var (json, digest) = SnapshotContentIdentity.Manifest(manifest);
        var snapshot = new ImmutableSnapshot(tenantId, snapshotId);
        Assert.True(snapshot.Freeze(snapshotId, null, programId, manifest, json,
            digest, null, Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow).IsSuccess);
        var frozen = Assert.Single(new AggregateScenario<ImmutableSnapshot>(snapshot).PendingEvents);
        var directory = new FitzSnapshotDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity("SnapshotDirectory",
            EventStreamPattern.ForPattern(tenantId.ToString()));

        // Act
        await using (var uncommitted = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
            await directory.ApplyAsync(frozen);
        var rolledBack = await directory.GetAsync(tenantId, snapshotId);
        await using (var committed = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(frozen);
            await committed.CommitAsync(ProjectionCheckpoint.Start);
        }
        var current = await directory.GetAsync(tenantId, snapshotId);
        var page = await directory.ListProgramAsync(tenantId, programId, 1, null);
        var foreign = await directory.GetAsync(otherTenantId, snapshotId);

        // Assert
        Assert.Null(rolledBack);
        Assert.Equal(digest, current?.ContentSha256);
        Assert.Equal(json, current?.CanonicalManifest);
        Assert.Equal(snapshotId, Assert.Single(page.Items).SnapshotId);
        Assert.Null(foreign);
    }

    [Fact]
    public async Task ShouldReportExactSourceStateGivenSnapshotVerification()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var versionId = Uuid.CreateVersion4();
        var snapshotId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var reviewerId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var plan = new ProgramPlan(null, null, null, null, null, null);
        var program = new ComplianceProgram(tenantId, programId);
        Assert.Null(program.Create("SOC 2", plan, actorId, "Lead", now));
        var programRevision = new ProgramRevisionView(programId, 1, "SOC 2", plan,
            actorId, "Lead", now);
        var boundary = new SystemBoundary(tenantId, boundaryId);
        var content = new BoundaryContent("Scope", "readiness", ["security"], []);
        Assert.True(boundary.Create(programId, versionId, content, actorId, "Lead", now).IsSuccess);
        var decisionId = Uuid.CreateVersion4();
        Assert.Null(boundary.Review(versionId, 1, decisionId, "accept", "Reviewed",
            reviewerId, "Reviewer", now));
        Assert.Null(boundary.Approve(versionId, 1, Uuid.CreateVersion4(), decisionId,
            new DateOnly(2027, 1, 1), "Approved", "impact", reviewerId, "Reviewer", now));
        var boundaryVersion = new BoundaryVersionView(tenantId, boundaryId, programId,
            versionId, 1, content, "approved", new DateOnly(2027, 1, 1), actorId,
            "Lead", now);
        var manifest = new ProgramScopeManifest(1, tenantId, programId, 1,
            SnapshotContentIdentity.ProgramRevision(programRevision), boundaryId, versionId,
            SnapshotContentIdentity.ApprovedBoundaryVersion(boundaryVersion));
        var (json, digest) = SnapshotContentIdentity.Manifest(manifest);
        var snapshot = new ImmutableSnapshot(tenantId, snapshotId);
        Assert.True(snapshot.Freeze(snapshotId, null, programId, manifest, json, digest,
            null, actorId, "Lead", now).IsSuccess);
        var view = new SnapshotView(tenantId, snapshotId, snapshotId, null, programId,
            "program_scope", 1, manifest, json, digest, null, actorId, "Lead", now);

        async Task<ProgramScopeSnapshotVerification> VerifyAsync(SnapshotView? projectedSnapshot,
            ProgramRevisionView? projectedProgram, BoundaryVersionView? projectedBoundary,
            ComplianceProgram? programSource = null, SystemBoundary? boundarySource = null)
        {
            var handler = new VerifyProgramScopeSnapshotHandler(
                new SnapshotDirectoryStub(projectedSnapshot),
                new OptionalProgramRevisionReader(projectedProgram),
                new LaggingBoundaryDirectory(projectedBoundary),
                new VerificationSourcesReader(snapshot, programSource ?? program,
                    boundarySource ?? boundary));
            var result = await handler.HandleAsync(
                Context(new VerifyProgramScopeSnapshot(tenantId, snapshotId)), CancellationToken.None);
            return Assert.IsType<ProgramScopeSnapshotVerification>(result.Value);
        }

        // Act
        var verified = await VerifyAsync(view, programRevision, boundaryVersion);
        var lag = await VerifyAsync(null, null, null);
        var digestMismatch = await VerifyAsync(view with { CanonicalManifest = json + " " },
            programRevision with { Name = "Changed" },
            boundaryVersion with { Content = content with { Statement = "Changed" } });
        var changedReason = await VerifyAsync(view with { AmendmentReason = "Changed" },
            programRevision, boundaryVersion);
        var changedActor = await VerifyAsync(view with { ActorMemberId = reviewerId },
            programRevision, boundaryVersion);
        var changedDisplay = await VerifyAsync(view with { ActorDisplay = "Changed" },
            programRevision, boundaryVersion);
        var changedFreezeTime = await VerifyAsync(view with { FrozenAt = now.AddMinutes(1) },
            programRevision, boundaryVersion);
        var staleApproval = await VerifyAsync(view, programRevision,
            boundaryVersion with { Status = "draft" });
        var missing = await VerifyAsync(view, programRevision, boundaryVersion,
            new ComplianceProgram(tenantId, programId),
            new SystemBoundary(tenantId, boundaryId));
        var inconsistent = await VerifyAsync(view,
            programRevision with { ProgramId = Uuid.CreateVersion4() },
            boundaryVersion with { TenantId = Uuid.CreateVersion4() });

        // Assert
        Assert.True(verified.Verified);
        Assert.Equal("verified", verified.Snapshot.Status);
        Assert.Equal("verified", verified.ProgramRevision.Status);
        Assert.Equal("verified", verified.ApprovedBoundaryVersion.Status);
        Assert.False(lag.Verified);
        Assert.Equal("lag", lag.Snapshot.Status);
        Assert.Equal("lag", lag.ProgramRevision.Status);
        Assert.Equal("lag", lag.ApprovedBoundaryVersion.Status);
        Assert.False(digestMismatch.Verified);
        Assert.Equal("digest_mismatch", digestMismatch.Snapshot.Status);
        Assert.Equal("digest_mismatch", digestMismatch.ProgramRevision.Status);
        Assert.Equal("digest_mismatch", digestMismatch.ApprovedBoundaryVersion.Status);
        Assert.All([changedReason, changedActor, changedDisplay, changedFreezeTime],
            result =>
            {
                Assert.False(result.Verified);
                Assert.Equal("digest_mismatch", result.Snapshot.Status);
            });
        Assert.Equal("lag", staleApproval.ApprovedBoundaryVersion.Status);
        Assert.False(missing.Verified);
        Assert.Equal("missing", missing.ProgramRevision.Status);
        Assert.Equal("missing", missing.ApprovedBoundaryVersion.Status);
        Assert.False(inconsistent.Verified);
        Assert.Equal("digest_mismatch", inconsistent.ProgramRevision.Status);
        Assert.Equal("digest_mismatch", inconsistent.ApprovedBoundaryVersion.Status);
        Assert.Null(inconsistent.ProgramRevision.ObservedContentSha256);
        Assert.Null(inconsistent.ApprovedBoundaryVersion.ObservedContentSha256);
        Assert.Equal(digest, verified.Snapshot.ExpectedContentSha256);
        Assert.Equal(manifest.ProgramContentSha256,
            verified.ProgramRevision.ObservedContentSha256);
    }

    [Fact]
    public async Task ShouldReportDigestMismatchGivenCorruptSnapshotSourceAndProjectionLag()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var snapshotId = Uuid.CreateVersion4();
        var manifest = Manifest(tenantId, programId);
        var (json, digest) = SnapshotContentIdentity.Manifest(manifest);
        var source = new ImmutableSnapshot(tenantId, snapshotId);
        new AggregateScenario<ImmutableSnapshot>(source).Given(DomainEventSeed.Attach(
            new SnapshotFrozen(tenantId, snapshotId, snapshotId, null, programId,
                "program_scope", manifest, json + " ", digest, null,
                Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow), snapshotId, 1));
        var handler = new VerifyProgramScopeSnapshotHandler(
            new SnapshotDirectoryStub(null), new OptionalProgramRevisionReader(null),
            new LaggingBoundaryDirectory(null),
            new VerificationSourcesReader(source,
                new ComplianceProgram(tenantId, programId),
                new SystemBoundary(tenantId, manifest.BoundaryId)));

        // Act
        var result = await handler.HandleAsync(
            Context(new VerifyProgramScopeSnapshot(tenantId, snapshotId)), CancellationToken.None);

        // Assert
        var verification = Assert.IsType<ProgramScopeSnapshotVerification>(result.Value);
        Assert.False(verification.Verified);
        Assert.Equal("digest_mismatch", verification.Snapshot.Status);
        Assert.Equal("missing", verification.ProgramRevision.Status);
        Assert.Equal("missing", verification.ApprovedBoundaryVersion.Status);
    }

    static ProgramScopeManifest Manifest(Uuid tenantId, Uuid programId) =>
        new(1, tenantId, programId, 1, new string('a', 64), Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), new string('b', 64));

    static List<ImmutableSnapshot> CreateLineage(Uuid tenantId, Uuid programId, Uuid actorId,
        Uuid rootId, ProgramScopeManifest manifest, string json, string digest,
        int amendmentCount)
    {
        var snapshots = new List<ImmutableSnapshot>();
        var root = new ImmutableSnapshot(tenantId, rootId);
        Assert.True(root.Freeze(rootId, null, programId, manifest, json, digest, null,
            actorId, "Lead", DateTimeOffset.UtcNow).IsSuccess);
        snapshots.Add(root);
        var predecessor = root;
        for (var amendmentNumber = 1; amendmentNumber <= amendmentCount; amendmentNumber++)
        {
            var amendment = new ImmutableSnapshot(tenantId, Uuid.CreateVersion4());
            Assert.True(amendment.Freeze(rootId, predecessor.Id, programId, manifest, json,
                digest, $"Correction {amendmentNumber}", actorId, "Lead",
                DateTimeOffset.UtcNow.AddMinutes(amendmentNumber)).IsSuccess);
            snapshots.Add(amendment);
            predecessor = amendment;
        }
        return snapshots;
    }

    sealed class SnapshotDirectoryStub(SnapshotView? view) : ISnapshotDirectoryReader
    {
        public ValueTask<SnapshotView?> GetAsync(Uuid tenantId, Uuid snapshotId,
            CancellationToken ct = default) => ValueTask.FromResult(view);

        public ValueTask<Page<SnapshotView>> ListProgramAsync(Uuid tenantId, Uuid programId,
            int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<SnapshotView>(view is null ? [] : [view], null));
    }

    sealed class SnapshotSourceReader(params ImmutableSnapshot[] sources) : IAggregateReader
    {
        readonly Dictionary<Uuid, ImmutableSnapshot> _sourceById = sources
            .ToDictionary(static source => source.Id);

        public int HydrationCount { get; private set; }

        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            HydrationCount++;
            return aggregate is ImmutableSnapshot snapshot && _sourceById.TryGetValue(snapshot.Id,
                out var source)
                ? ValueTask.FromResult((TAggregate)(Aggregate)source)
                : ValueTask.FromResult(aggregate);
        }
    }

    sealed class SnapshotSourcesReader(ComplianceProgram program, SystemBoundary boundary)
        : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult((TAggregate)(Aggregate)(aggregate is ComplianceProgram
                ? program : boundary));
    }

    sealed class SnapshotCreationSourcesReader(ComplianceProgram program, SystemBoundary boundary,
        IEnumerable<ImmutableSnapshot> snapshots) : IAggregateReader
    {
        readonly Dictionary<Uuid, ImmutableSnapshot> _snapshots = snapshots.ToDictionary(
            static snapshot => snapshot.Id);

        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            aggregate switch
            {
                ImmutableSnapshot snapshot when _snapshots.TryGetValue(snapshot.Id,
                    out var retained) => ValueTask.FromResult((TAggregate)(Aggregate)retained),
                ComplianceProgram => ValueTask.FromResult((TAggregate)(Aggregate)program),
                SystemBoundary => ValueTask.FromResult((TAggregate)(Aggregate)boundary),
                _ => ValueTask.FromResult(aggregate),
            };
    }

    sealed class ProgramRevisionReader(ProgramRevisionView revision) : IProgramDirectoryReader
    {
        public ValueTask<ProgramView?> GetAsync(Uuid tenantId, Uuid programId,
            CancellationToken ct = default) => throw new NotImplementedException();
        public ValueTask<Page<ProgramView>> ListAsync(Uuid tenantId, int limit,
            string? cursor, CancellationToken ct = default) => throw new NotImplementedException();
        public ValueTask<Page<ProgramRevisionView>?> ListRevisionsAsync(Uuid tenantId,
            Uuid programId, int limit, string? cursor, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public ValueTask<ProgramRevisionView?> GetRevisionAsync(Uuid tenantId,
            Uuid programId, long requestedRevision, CancellationToken ct = default) =>
            ValueTask.FromResult<ProgramRevisionView?>(revision);

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);
    }

    sealed class OptionalProgramRevisionReader(ProgramRevisionView? revision)
        : IProgramDirectoryReader
    {
        public ValueTask<ProgramView?> GetAsync(Uuid tenantId, Uuid programId,
            CancellationToken ct = default) => throw new NotImplementedException();
        public ValueTask<Page<ProgramView>> ListAsync(Uuid tenantId, int limit,
            string? cursor, CancellationToken ct = default) => throw new NotImplementedException();
        public ValueTask<Page<ProgramRevisionView>?> ListRevisionsAsync(Uuid tenantId,
            Uuid programId, int limit, string? cursor, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public ValueTask<ProgramRevisionView?> GetRevisionAsync(Uuid tenantId,
            Uuid programId, long requestedRevision, CancellationToken ct = default) =>
            ValueTask.FromResult(revision);

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);
    }

    sealed class VerificationSourcesReader(ImmutableSnapshot snapshot, ComplianceProgram program,
        SystemBoundary boundary) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult((TAggregate)(Aggregate)(aggregate switch
            {
                ImmutableSnapshot => snapshot,
                ComplianceProgram => program,
                SystemBoundary => boundary,
                _ => throw new NotSupportedException(),
            }));
    }

    sealed class LaggingBoundaryDirectory(BoundaryVersionView? version)
        : IBoundaryDirectoryReader
    {
        public ValueTask<BoundaryView?> GetAsync(Uuid tenantId, Uuid boundaryId,
            CancellationToken ct = default) => throw new NotImplementedException();
        public ValueTask<Page<BoundaryView>> ListProgramAsync(Uuid tenantId, Uuid programId,
            int limit, string? cursor, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public ValueTask<BoundaryVersionView?> GetVersionAsync(Uuid tenantId, Uuid boundaryId,
            Uuid versionId, CancellationToken ct = default) => ValueTask.FromResult(version);
        public ValueTask<Page<BoundaryVersionView>?> ListVersionsAsync(Uuid tenantId,
            Uuid boundaryId, int limit, string? cursor, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public ValueTask<BoundaryVersionView?> GetEffectiveVersionAsync(Uuid tenantId,
            Uuid boundaryId, DateOnly effectiveOn, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public ValueTask<BoundaryDecisionView?> GetDecisionAsync(Uuid tenantId,
            Uuid boundaryId, Uuid decisionId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public ValueTask<Page<BoundaryDecisionView>?> ListDecisionsAsync(Uuid tenantId,
            Uuid boundaryId, int limit, string? cursor, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);
    }

    static RequestContext<T> Context<T>(T request) where T : IRequestBase =>
        new(request, new ClaimsPrincipal());
}

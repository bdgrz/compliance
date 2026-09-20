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
            new SnapshotRequestContext<GetSnapshot>(new GetSnapshot(tenantId, snapshotId, 1)),
            CancellationToken.None);
        var corrupt = await new GetSnapshotHandler(new SnapshotDirectoryStub(corrupted),
            new SnapshotSourceReader(source)).HandleAsync(
            new SnapshotRequestContext<GetSnapshot>(new GetSnapshot(tenantId, snapshotId)),
            CancellationToken.None);
        var foreign = await new GetSnapshotHandler(new SnapshotDirectoryStub(
                corrupted with { TenantId = Uuid.CreateVersion4() }),
            new SnapshotSourceReader(source)).HandleAsync(
            new SnapshotRequestContext<GetSnapshot>(new GetSnapshot(tenantId, snapshotId)),
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
        Assert.True(program.Create("SOC 2", plan, authorId, "Lead", now).IsSuccess);
        var boundary = new SystemBoundary(tenantId, boundaryId);
        var content = new BoundaryContent("Scope", "readiness", ["security"], []);
        Assert.True(boundary.Create(programId, firstVersionId, content,
            authorId, "Lead", now).IsSuccess);
        var firstDecisionId = Uuid.CreateVersion4();
        Assert.True(boundary.Review(firstVersionId, 1, firstDecisionId, "accept", "Reviewed",
            reviewerId, "Reviewer", now).IsSuccess);
        Assert.True(boundary.Approve(firstVersionId, 1, Uuid.CreateVersion4(), firstDecisionId,
            new DateOnly(2027, 1, 1), "Approved", "impact-1", reviewerId, "Reviewer", now)
            .IsSuccess);
        Assert.True(boundary.ProposeSuccessor(firstVersionId, secondVersionId, content,
            authorId, "Lead", now).IsSuccess);
        var secondDecisionId = Uuid.CreateVersion4();
        Assert.True(boundary.Review(secondVersionId, 1, secondDecisionId, "accept", "Reviewed",
            reviewerId, "Reviewer", now).IsSuccess);
        Assert.True(boundary.Approve(secondVersionId, 1, Uuid.CreateVersion4(), secondDecisionId,
            new DateOnly(2027, 2, 1), "Approved", "impact-2", reviewerId, "Reviewer", now)
            .IsSuccess);
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
        var result = await freezer.FreezeAsync(new SnapshotRequestContext<FreezeProgramScopeSnapshot>(
            request), tenantId, programId, 1, boundaryId, firstVersionId,
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
        Assert.True(program.Create("SOC 2", plan, actorId, "Lead", now).IsSuccess);
        var programRevision = new ProgramRevisionView(programId, 1, "SOC 2", plan,
            actorId, "Lead", now);
        var boundary = new SystemBoundary(tenantId, boundaryId);
        var content = new BoundaryContent("Scope", "readiness", ["security"], []);
        Assert.True(boundary.Create(programId, versionId, content, actorId, "Lead", now).IsSuccess);
        var decisionId = Uuid.CreateVersion4();
        Assert.True(boundary.Review(versionId, 1, decisionId, "accept", "Reviewed",
            reviewerId, "Reviewer", now).IsSuccess);
        Assert.True(boundary.Approve(versionId, 1, Uuid.CreateVersion4(), decisionId,
            new DateOnly(2027, 1, 1), "Approved", "impact", reviewerId, "Reviewer", now)
            .IsSuccess);
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
                new SnapshotRequestContext<VerifyProgramScopeSnapshot>(
                    new VerifyProgramScopeSnapshot(tenantId, snapshotId)), CancellationToken.None);
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
            new SnapshotRequestContext<VerifyProgramScopeSnapshot>(
                new VerifyProgramScopeSnapshot(tenantId, snapshotId)), CancellationToken.None);

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

    sealed class SnapshotDirectoryStub(SnapshotView? view) : ISnapshotDirectoryReader
    {
        public ValueTask<SnapshotView?> GetAsync(Uuid tenantId, Uuid snapshotId,
            CancellationToken ct = default) => ValueTask.FromResult(view);

        public ValueTask<Page<SnapshotView>> ListProgramAsync(Uuid tenantId, Uuid programId,
            int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<SnapshotView>(view is null ? [] : [view], null));
    }

    sealed class SnapshotSourceReader(Aggregate source) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult((TAggregate)source);
    }

    sealed class SnapshotSourcesReader(ComplianceProgram program, SystemBoundary boundary)
        : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult((TAggregate)(Aggregate)(aggregate is ComplianceProgram
                ? program : boundary));
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
    }

    sealed class SnapshotRequestContext<TRequest>(TRequest request) : IRequestContext<TRequest>
    {
        public TRequest Request { get; } = request;
        public ClaimsPrincipal Actor { get; } = new();
        public Uuid ExecutionId { get; } = Uuid.CreateVersion4();
        public Uuid RequestId { get; } = Uuid.CreateVersion4();
        public Uuid CorrelationId { get; } = Uuid.CreateVersion4();
        public Uuid? CausationId => null;
        public Uuid CauseId => RequestId;
        public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
        public RequestInvocation Invocation { get; } = new DirectInvocation();
    }
}

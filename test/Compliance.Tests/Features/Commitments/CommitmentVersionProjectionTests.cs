using Bdgrz.Compliance.Features.Commitments;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Commitments;

public sealed class CommitmentVersionProjectionTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid AuthorId = Uuid.CreateVersion4();
    static readonly Uuid ReviewerId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);
    static readonly Uuid DraftId = CommitmentDraft.IdFor(TenantId, ProgramId,
        "user_entity_responsibility", "CUEC-01");

    [Fact]
    public async Task ShouldProjectImmutableVersionAndDecisionsGivenReviewHistory()
    {
        // Arrange
        var directory = new FitzCommitmentDraftDirectory(new InMemoryKvClient());
        var changesRequested = Reviewed(1, "request_changes", null, null);
        var accepted = Reviewed(2, "accept", 1, new DateOnly(2027, 1, 1));
        await ProjectAsync(directory,
            new CommitmentDraftCreated(TenantId, ProgramId, DraftId, Uuid.CreateVersion4(),
                Uuid.CreateVersion4(), "user_entity_responsibility", "CUEC-01", "First",
                "Context", "MSA 4.1", AuthorId, "Author", Now),
            changesRequested,
            new CommitmentDraftRevised(TenantId, ProgramId, DraftId, 2, "Second", "Context",
                "MSA 4.2", AuthorId, "Author", Now.AddMinutes(1)),
            accepted);

        // Act
        var version = await directory.GetVersionAsync(TenantId, DraftId, 1);
        var versions = await directory.ListVersionsAsync(TenantId, DraftId, 50, null);
        var decisions = await directory.ListDecisionsAsync(TenantId, DraftId, 50, null);
        var draft = await directory.GetAsync(TenantId, DraftId);

        // Assert
        Assert.NotNull(version);
        Assert.Equal("Second", version.Statement);
        Assert.Equal(2, version.Revision);
        Assert.Equal("user_entity", version.PerformedBy);
        Assert.False(version.InternallyPerformed);
        Assert.Equal(accepted.DecisionId, version.Decision.DecisionId);
        Assert.Single(versions.Items);
        Assert.Equal(2, decisions.Items.Count);
        Assert.Equal("effective", draft?.Status);
        Assert.Equal("verified", draft?.OwnerResolution);
        Assert.Equal("applicable", draft?.ApplicabilityResolution);
    }

    [Fact]
    public async Task ShouldProjectApprovedVersionWithVerifiedSourceGivenSeparateApproval()
    {
        // Arrange
        var directory = new FitzCommitmentDraftDirectory(new InMemoryKvClient());
        var reviewed = new CommitmentReviewed(TenantId, ProgramId, DraftId, 1,
            Uuid.CreateVersion4(), "accept", "Customer IT", "applicable", "supported", null,
            "Verified", null, null, null, ReviewerId, "Reviewer", Now.AddMinutes(2),
            SourceVerification: "verified", SourceVerifiedReference: "MSA 4.1",
            SourceEvidence: "Signed MSA v3");
        var approverId = Uuid.CreateVersion4();
        var approved = new CommitmentApproved(TenantId, ProgramId, DraftId, 1,
            Uuid.CreateVersion4(), reviewed.DecisionId, 1, new DateOnly(2027, 1, 1), "digest",
            "Approved", approverId, "Approver", Now.AddMinutes(3));
        await ProjectAsync(directory,
            new CommitmentDraftCreated(TenantId, ProgramId, DraftId, Uuid.CreateVersion4(),
                Uuid.CreateVersion4(), "user_entity_responsibility", "CUEC-01", "First",
                "Context", "MSA 4.1", AuthorId, "Author", Now),
            reviewed);
        var pending = await directory.GetAsync(TenantId, DraftId);
        await ProjectAsync(directory, approved);

        // Act
        var version = await directory.GetVersionAsync(TenantId, DraftId, 1);
        var decisions = await directory.ListDecisionsAsync(TenantId, DraftId, 50, null);
        var draft = await directory.GetAsync(TenantId, DraftId);

        // Assert
        Assert.Equal("reviewed", pending?.Status);
        Assert.Equal("verified", pending?.SourceResolution);
        Assert.NotNull(version);
        Assert.Equal("verified", version.SourceResolution);
        Assert.Equal("Signed MSA v3", version.SourceEvidence);
        Assert.Equal(reviewed.DecisionId, version.Decision.DecisionId);
        Assert.Equal(approved.DecisionId, version.Approval?.DecisionId);
        Assert.Equal("approval", version.Approval?.Stage);
        Assert.Equal(2, decisions.Items.Count);
        Assert.Equal("effective", draft?.Status);
    }

    static CommitmentReviewed Reviewed(long revision, string outcome, long? version,
        DateOnly? effectiveFrom) =>
        new(TenantId, ProgramId, DraftId, revision, Uuid.CreateVersion4(), outcome,
            version is null ? null : "Customer IT", version is null ? null : "applicable",
            version is null ? null : "supported", null, "Rationale", version, effectiveFrom,
            version is null ? null : "digest", ReviewerId, "Reviewer", Now.AddMinutes(revision * 2));

    static async Task ProjectAsync(FitzCommitmentDraftDirectory store,
        params DomainEvent[] events)
    {
        await using var batch = await store.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity("CommitmentDraftDirectory", EventStreamPattern.ForPattern(
                TenantId.ToString(), "commitment-drafts")), ProjectionCheckpoint.Start));
        foreach (var domainEvent in events)
            await store.ApplyAsync(domainEvent, CancellationToken.None);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }
}

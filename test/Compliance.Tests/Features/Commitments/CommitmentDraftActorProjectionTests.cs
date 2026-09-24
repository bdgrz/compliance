using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Commitments;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Commitments;

public sealed class CommitmentDraftActorProjectionTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid MemberId = Uuid.CreateVersion4();
    static readonly Uuid DraftId = CommitmentDraft.IdFor(TenantId, ProgramId, "service_commitment", "SC-01");
    static readonly ActorReference Process =
        ActorReference.ForSystemProcess("reactor:commitment-import", "commitment-import");

    [Fact]
    public async Task ShouldProjectMemberActorGivenLegacyCreatedEventInDraftDirectory()
    {
        // Arrange
        var directory = new FitzCommitmentDraftDirectory(new InMemoryKvClient());
        await ProjectAsync(directory, directory.ApplyAsync, "CommitmentDraftDirectory", LegacyCreated());

        // Act
        var view = await directory.GetAsync(TenantId, DraftId);

        // Assert
        Assert.Equal(ActorReference.ForMember(MemberId, "Legacy display"), view?.LastChangedBy);
    }

    [Fact]
    public async Task ShouldProjectSnapshottedActorGivenSnapshottedCreatedEventInDraftDirectory()
    {
        // Arrange
        var directory = new FitzCommitmentDraftDirectory(new InMemoryKvClient());
        await ProjectAsync(directory, directory.ApplyAsync, "CommitmentDraftDirectory",
            LegacyCreated() with { StoredActor = Process });

        // Act
        var view = await directory.GetAsync(TenantId, DraftId);

        // Assert
        Assert.Equal(Process, view?.LastChangedBy);
    }

    [Fact]
    public async Task ShouldProjectMemberActorGivenLegacyCreatedEventInHistoryDirectory()
    {
        // Arrange
        var directory = new FitzCommitmentDraftHistoryDirectoryV1(new InMemoryKvClient());
        await ProjectAsync(directory, directory.ApplyAsync, "CommitmentDraftHistoryDirectoryV1", LegacyCreated());

        // Act
        var view = await directory.GetRevisionAsync(TenantId, DraftId, 1);

        // Assert
        Assert.Equal(ActorReference.ForMember(MemberId, "Legacy display"), view?.Actor);
    }

    [Fact]
    public async Task ShouldProjectSnapshottedActorGivenSnapshottedCreatedEventInHistoryDirectory()
    {
        // Arrange
        var directory = new FitzCommitmentDraftHistoryDirectoryV1(new InMemoryKvClient());
        await ProjectAsync(directory, directory.ApplyAsync, "CommitmentDraftHistoryDirectoryV1",
            LegacyCreated() with { StoredActor = Process });

        // Act
        var view = await directory.GetRevisionAsync(TenantId, DraftId, 1);

        // Assert
        Assert.Equal(Process, view?.Actor);
    }

    static CommitmentDraftCreated LegacyCreated() => Assert.IsType<CommitmentDraftCreated>(
        JsonSerializer.Deserialize($$"""
            {"tenant_id":"{{TenantId}}","program_id":"{{ProgramId}}","draft_id":"{{DraftId}}","create_request_id":"{{Uuid.CreateVersion4()}}","service_id":"{{Uuid.CreateVersion4()}}","kind":"service_commitment","identifier":"SC-01","statement":"First statement","context":"Draft context","source_reference":"Source A","actor_member_id":"{{MemberId}}","actor_display":"Legacy display","changed_at":"2026-09-23T12:00:00+00:00"}
            """, ComplianceCoreJsonContext.Default.CommitmentDraftCreated));

    static async Task ProjectAsync(IProjectionStore store,
        Func<DomainEvent, CancellationToken, ValueTask> apply, string name,
        DomainEvent domainEvent)
    {
        await using var batch = await store.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity(name, EventStreamPattern.ForPattern(TenantId.ToString(),
                "commitment-drafts")), ProjectionCheckpoint.Start));
        await apply(domainEvent, CancellationToken.None);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }
}

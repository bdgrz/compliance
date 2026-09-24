using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Controls;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Controls;

public sealed class ControlDraftActorProjectionTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid MemberId = Uuid.CreateVersion4();
    static readonly Uuid ControlId = ControlDraft.IdFor(TenantId, ProgramId, "AC-01");
    static readonly ActorReference Process =
        ActorReference.ForSystemProcess("reactor:control-import", "control-import");

    [Fact]
    public async Task ShouldProjectMemberActorGivenLegacyCreatedEventInDraftDirectory()
    {
        // Arrange
        var directory = new FitzControlDraftDirectoryV2(new InMemoryKvClient());
        await ProjectAsync(directory, directory.ApplyAsync, "ControlDraftDirectoryV2", LegacyCreated());

        // Act
        var view = await directory.GetAsync(TenantId, ControlId);

        // Assert
        Assert.Equal(ActorReference.ForMember(MemberId, "Legacy display"), view?.LastChangedBy);
    }

    [Fact]
    public async Task ShouldProjectSnapshottedActorGivenSnapshottedCreatedEventInDraftDirectory()
    {
        // Arrange
        var directory = new FitzControlDraftDirectoryV2(new InMemoryKvClient());
        await ProjectAsync(directory, directory.ApplyAsync, "ControlDraftDirectoryV2",
            LegacyCreated() with { StoredActor = Process });

        // Act
        var view = await directory.GetAsync(TenantId, ControlId);

        // Assert
        Assert.Equal(Process, view?.LastChangedBy);
    }

    [Fact]
    public async Task ShouldProjectMemberActorGivenLegacyCreatedEventInHistoryDirectory()
    {
        // Arrange
        var directory = new FitzControlDraftHistoryDirectoryV1(new InMemoryKvClient());
        await ProjectAsync(directory, directory.ApplyAsync, "ControlDraftHistoryDirectoryV1", LegacyCreated());

        // Act
        var view = await directory.GetRevisionAsync(TenantId, ControlId, 1);

        // Assert
        Assert.Equal(ActorReference.ForMember(MemberId, "Legacy display"), view?.Actor);
    }

    [Fact]
    public async Task ShouldProjectSnapshottedActorGivenSnapshottedCreatedEventInHistoryDirectory()
    {
        // Arrange
        var directory = new FitzControlDraftHistoryDirectoryV1(new InMemoryKvClient());
        await ProjectAsync(directory, directory.ApplyAsync, "ControlDraftHistoryDirectoryV1",
            LegacyCreated() with { StoredActor = Process });

        // Act
        var view = await directory.GetRevisionAsync(TenantId, ControlId, 1);

        // Assert
        Assert.Equal(Process, view?.Actor);
    }

    static ControlDraftCreated LegacyCreated() => Assert.IsType<ControlDraftCreated>(
        JsonSerializer.Deserialize($$"""
            {"tenant_id":"{{TenantId}}","program_id":"{{ProgramId}}","control_id":"{{ControlId}}","create_request_id":"{{Uuid.CreateVersion4()}}","identifier":"AC-01","content":{"title":"Access review","objective":"Review access","description":"Management reviews access","implementation_narrative":"The owner reviews access quarterly.","expected_evidence_descriptions":["Dated review record"]},"actor_member_id":"{{MemberId}}","actor_display":"Legacy display","changed_at":"2026-09-23T12:00:00+00:00"}
            """, ComplianceCoreJsonContext.Default.ControlDraftCreated));

    static async Task ProjectAsync(IProjectionStore store,
        Func<DomainEvent, CancellationToken, ValueTask> apply, string name,
        DomainEvent domainEvent)
    {
        await using var batch = await store.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity(name, EventStreamPattern.ForPattern(TenantId.ToString(),
                "controls")), ProjectionCheckpoint.Start));
        await apply(domainEvent, CancellationToken.None);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }
}

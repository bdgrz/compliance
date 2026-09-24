using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Risks;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Risks;

public sealed class RiskDraftActorProjectionTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid MemberId = Uuid.CreateVersion4();
    static readonly Uuid RiskId = RiskDraft.IdFor(TenantId, ProgramId, "R-01");
    static readonly ActorReference Process =
        ActorReference.ForSystemProcess("reactor:risk-import", "risk-import");

    [Fact]
    public async Task ShouldProjectMemberActorGivenLegacyCreatedEventInDraftDirectory()
    {
        // Arrange
        var directory = new FitzRiskDraftDirectory(new InMemoryKvClient());
        await ProjectAsync(directory, directory.ApplyAsync, "RiskDraftDirectory", LegacyCreated());

        // Act
        var view = await directory.GetAsync(TenantId, RiskId);

        // Assert
        Assert.Equal(ActorReference.ForMember(MemberId, "Legacy display"), view?.LastChangedBy);
    }

    [Fact]
    public async Task ShouldProjectSnapshottedActorGivenSnapshottedCreatedEventInDraftDirectory()
    {
        // Arrange
        var directory = new FitzRiskDraftDirectory(new InMemoryKvClient());
        await ProjectAsync(directory, directory.ApplyAsync, "RiskDraftDirectory",
            LegacyCreated() with { StoredActor = Process });

        // Act
        var view = await directory.GetAsync(TenantId, RiskId);

        // Assert
        Assert.Equal(Process, view?.LastChangedBy);
    }

    [Fact]
    public async Task ShouldProjectMemberActorGivenLegacyCreatedEventInHistoryDirectory()
    {
        // Arrange
        var directory = new FitzRiskDraftHistoryDirectoryV1(new InMemoryKvClient());
        await ProjectAsync(directory, directory.ApplyAsync, "RiskDraftHistoryDirectoryV1", LegacyCreated());

        // Act
        var view = await directory.GetRevisionAsync(TenantId, RiskId, 1);

        // Assert
        Assert.Equal(ActorReference.ForMember(MemberId, "Legacy display"), view?.Actor);
    }

    [Fact]
    public async Task ShouldProjectSnapshottedActorGivenSnapshottedCreatedEventInHistoryDirectory()
    {
        // Arrange
        var directory = new FitzRiskDraftHistoryDirectoryV1(new InMemoryKvClient());
        await ProjectAsync(directory, directory.ApplyAsync, "RiskDraftHistoryDirectoryV1",
            LegacyCreated() with { StoredActor = Process });

        // Act
        var view = await directory.GetRevisionAsync(TenantId, RiskId, 1);

        // Assert
        Assert.Equal(Process, view?.Actor);
    }

    static RiskDraftCreated LegacyCreated() => Assert.IsType<RiskDraftCreated>(
        JsonSerializer.Deserialize($$"""
            {"tenant_id":"{{TenantId}}","program_id":"{{ProgramId}}","risk_id":"{{RiskId}}","create_request_id":"{{Uuid.CreateVersion4()}}","identifier":"R-01","content":{"title":"Provider outage","scenario":"Provider unavailable","potential_effect":"Requests cannot be processed","source_note":"Management note"},"actor_member_id":"{{MemberId}}","actor_display":"Legacy display","changed_at":"2026-09-23T12:00:00+00:00"}
            """, ComplianceCoreJsonContext.Default.RiskDraftCreated));

    static async Task ProjectAsync(IProjectionStore store,
        Func<DomainEvent, CancellationToken, ValueTask> apply, string name,
        DomainEvent domainEvent)
    {
        await using var batch = await store.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity(name, EventStreamPattern.ForPattern(TenantId.ToString(),
                "risks")), ProjectionCheckpoint.Start));
        await apply(domainEvent, CancellationToken.None);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }
}

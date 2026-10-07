using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Work;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class FitzFindingClosureWorkItemDirectoryTests
{
    readonly Uuid _tenant = Uuid.CreateVersion4();
    readonly Uuid _program = Uuid.CreateVersion4();
    readonly Uuid _finding = Uuid.CreateVersion4();
    readonly Uuid _owner = Uuid.CreateVersion4();
    readonly Uuid _actionOwner = Uuid.CreateVersion4();
    readonly Uuid _completer = Uuid.CreateVersion4();
    readonly Uuid _action = Uuid.CreateVersion4();
    readonly DateTimeOffset _now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldProjectExactClosureRoundGivenCompletedActionsAndRevisionChanges()
    {
        // Arrange
        var directory = new FitzFindingClosureWorkItemDirectory(new InMemoryKvClient());
        var checkpoint = new ProjectionCheckpoint(new EventCursor("finding-closure-3"));
        await ApplyAsync(directory, ProjectionCheckpoint.Start, checkpoint, Raised(), Added(), Completed());

        // Act
        var pending = await directory.LoadProgramAsync(_tenant, _program);
        var first = Assert.Single(pending.Value);
        await ApplyAsync(directory, checkpoint, checkpoint, new FindingRevised(_tenant, _program,
            _finding, 4, "medium", _owner, DueOn(), "VPN", null, "Reassessed severity.", Actor(),
            _now.AddMinutes(3)));
        var revised = await directory.LoadProgramAsync(_tenant, _program);

        // Assert
        var second = Assert.Single(revised.Value);
        Assert.NotEqual(first.WorkItemId, second.WorkItemId);
        Assert.Equal(FindingClosureWork.Kind, second.Kind);
        Assert.Equal(_finding, second.SourceId);
        Assert.Equal(_finding, second.FindingId);
        Assert.Equal("medium", second.Materiality);
        Assert.Equal(DueOn(), second.DueOn);
        Assert.Equal("close", second.NextAction);
        Assert.Contains("revision 4", second.Reason, StringComparison.Ordinal);
        Assert.True(second.Excluded.SetEquals([_owner, _actionOwner, _completer]));
        Assert.Equal(checkpoint, await directory.LoadCheckpointAsync(_tenant));
    }

    [Fact]
    public async Task ShouldRemoveClosureWorkGivenClosureAndCreateNewRoundGivenReopening()
    {
        // Arrange
        var directory = new FitzFindingClosureWorkItemDirectory(new InMemoryKvClient());
        await ApplyAsync(directory, ProjectionCheckpoint.Start, ProjectionCheckpoint.Start,
            Raised(), Added(), Completed());
        var before = Assert.Single((await directory.LoadProgramAsync(_tenant, _program)).Value);
        var closure = new FindingClosureView(Uuid.CreateVersion4(), "Verified independently.", [],
            "All corrected.", Uuid.CreateVersion4(), Actor(), _now.AddMinutes(3), null);

        // Act
        await ApplyAsync(directory, ProjectionCheckpoint.Start, ProjectionCheckpoint.Start,
            new FindingClosed(_tenant, _program, _finding, 4, closure));
        var closed = await directory.LoadProgramAsync(_tenant, _program);
        await ApplyAsync(directory, ProjectionCheckpoint.Start, ProjectionCheckpoint.Start,
            new FindingReopened(_tenant, _program, _finding, 5, "Verify new source facts.",
                Actor(), _now.AddMinutes(4)));
        var reopened = await directory.LoadProgramAsync(_tenant, _program);

        // Assert
        Assert.Empty(closed.Value);
        var after = Assert.Single(reopened.Value);
        Assert.NotEqual(before.WorkItemId, after.WorkItemId);
        Assert.Equal(_now.AddMinutes(4), after.CreatedAt);
        Assert.Contains("revision 5", after.Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldOmitClosureWorkGivenNoActionsOrIncompleteActions(bool addAction)
    {
        // Arrange
        var directory = new FitzFindingClosureWorkItemDirectory(new InMemoryKvClient());
        var events = addAction ? new DomainEvent[] { Raised(), Added() } : [Raised()];
        await ApplyAsync(directory, ProjectionCheckpoint.Start, ProjectionCheckpoint.Start, events);

        // Act
        var result = await directory.LoadProgramAsync(_tenant, _program);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task ShouldRemoveClosureRoundGivenNewCorrectiveAction()
    {
        // Arrange
        var directory = new FitzFindingClosureWorkItemDirectory(new InMemoryKvClient());
        await ApplyAsync(directory, ProjectionCheckpoint.Start, ProjectionCheckpoint.Start,
            Raised(), Added(), Completed());
        Assert.Single((await directory.LoadProgramAsync(_tenant, _program)).Value);

        // Act
        await ApplyAsync(directory, ProjectionCheckpoint.Start, ProjectionCheckpoint.Start,
            new CorrectiveActionAdded(_tenant, _program, _finding, 4, Added().Action with
            {
                ActionId = Uuid.CreateVersion4(),
                Description = "Additional verification work.",
            }));
        var result = await directory.LoadProgramAsync(_tenant, _program);

        // Assert
        Assert.Empty(result.Value);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("program")]
    [InlineData("revision")]
    [InlineData("action")]
    [InlineData("empty_member")]
    public async Task ShouldRollBackWorkAndCheckpointGivenInvalidSourceEvent(string change)
    {
        // Arrange
        var directory = new FitzFindingClosureWorkItemDirectory(new InMemoryKvClient());
        var checkpoint = new ProjectionCheckpoint(new EventCursor("before-invalid"));
        await ApplyAsync(directory, ProjectionCheckpoint.Start, checkpoint, Raised(), Added());
        var completion = Completed();
        completion = change switch
        {
            "tenant" => completion with { TenantId = Uuid.CreateVersion4() },
            "program" => completion with { ProgramId = Uuid.CreateVersion4() },
            "revision" => completion with { Revision = 4 },
            "empty_member" => completion with { CompletedByMemberId = Uuid.Empty },
            _ => completion with { ActionId = Uuid.CreateVersion4() },
        };

        // Act
        var error = await Record.ExceptionAsync(() => ApplyAsync(directory, checkpoint,
            new ProjectionCheckpoint(new EventCursor("invalid")), completion));

        // Assert
        Assert.IsType<InvalidOperationException>(error);
        Assert.Equal(checkpoint, await directory.LoadCheckpointAsync(_tenant));
        Assert.Empty((await directory.LoadProgramAsync(_tenant, _program)).Value);
    }

    [Fact]
    public async Task ShouldIsolateScopeAndReadAllPagesGivenManyClosureItems()
    {
        // Arrange
        var directory = new FitzFindingClosureWorkItemDirectory(new InMemoryKvClient());
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         Identity(), ProjectionCheckpoint.Start)))
        {
            for (var index = 0; index < 205; index++)
            {
                var finding = Uuid.CreateVersion4();
                await directory.ApplyAsync(Raised() with { FindingId = finding });
                await directory.ApplyAsync(Added() with { FindingId = finding });
                await directory.ApplyAsync(Completed() with { FindingId = finding });
            }
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var result = await directory.LoadProgramAsync(_tenant, _program);
        var foreignTenant = await directory.LoadProgramAsync(Uuid.CreateVersion4(), _program);
        var foreignProgram = await directory.LoadProgramAsync(_tenant, Uuid.CreateVersion4());

        // Assert
        Assert.Equal(205, result.Value.Count);
        Assert.Equal(205, result.Value.Select(static item => item.WorkItemId).Distinct().Count());
        Assert.Empty(foreignTenant.Value);
        Assert.Empty(foreignProgram.Value);
    }

    async Task ApplyAsync(FitzFindingClosureWorkItemDirectory directory,
        ProjectionCheckpoint previous, ProjectionCheckpoint next, params DomainEvent[] events)
    {
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(Identity(), previous));
        foreach (var ev in events)
            await directory.ApplyAsync(ev);
        await batch.CommitAsync(next);
    }

    CheckpointIdentity Identity() => new(FitzFindingClosureWorkItemDirectory.ProjectorName,
        EventStreamPattern.ForPattern(_tenant.ToString(), "remediation"));
    static ActorReference Actor() => ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
    static DateOnly DueOn() => new(2026, 10, 9);
    FindingRaised Raised() => new(_tenant, _program, _finding, 1,
        new FindingSource("manual", null, null, "Stale access."), "Stale access", "Old accounts.",
        "high", "VPN", _owner, DueOn(), [], Actor(), _now);
    CorrectiveActionAdded Added() => new(_tenant, _program, _finding, 2,
        new CorrectiveActionView(_action, "Remove accounts", _actionOwner, DueOn(),
            RemediationLedger.Open, Actor(), _now.AddMinutes(1)));
    CorrectiveActionCompleted Completed() => new(_tenant, _program, _finding, 3,
        _action, "Removed", [], _completer, Actor(), _now.AddMinutes(2));
}

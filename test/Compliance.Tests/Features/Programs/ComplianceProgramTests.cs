using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Programs;

public sealed class ComplianceProgramTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid MemberId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateAndRevisePreserveRevisionEventsAndActorSnapshot()
    {
        var program = new ComplianceProgram(TenantId, ProgramId);
        var original = new ProgramPlan(null, new DateOnly(2027, 3, 1),
            new DateOnly(2027, 4, 1), new DateOnly(2028, 3, 31), "Advisor A", null);
        var revised = original with { ReadinessAdvisor = "Advisor B" };

        var created = program.Create("SOC 2", original, MemberId, "Lead A", Now);
        var changed = program.Revise(1, "SOC 2 program", revised, MemberId, "Lead B", Now.AddDays(1));
        var events = new AggregateScenario<ComplianceProgram>(program).PendingEvents;

        Assert.True(created.IsSuccess);
        Assert.Equal(ProgramId, created.Value.ProgramId);
        Assert.True(changed.IsSuccess);
        Assert.Collection(events,
            ev => Assert.Equal("Lead A", Assert.IsType<ProgramCreated>(ev).ActorDisplay),
            ev =>
            {
                Assert.Equal("ProgramRevised", ev.GetType().Name);
                Assert.Equal(2L, ev.GetType().GetProperty("Revision")?.GetValue(ev));
                Assert.Equal("Lead B", ev.GetType().GetProperty("ActorDisplay")?.GetValue(ev));
            });
    }

    [Fact]
    public void StaleRevisionAndInvalidTargetPeriodDoNotAppendEvents()
    {
        var program = new ComplianceProgram(TenantId, ProgramId);
        var valid = new ProgramPlan(null, null, null, null, null, null);
        Assert.True(program.Create("SOC 2", valid, MemberId, "Lead", Now).IsSuccess);
        var invalid = valid with
        {
            TargetTypeIIStartDate = new DateOnly(2028, 4, 1),
            TargetTypeIIEndDate = new DateOnly(2028, 3, 1),
        };
        var reversedJourney = valid with
        {
            TargetReadinessDate = new DateOnly(2028, 4, 1),
            TargetTypeIAsOfDate = new DateOnly(2028, 3, 1),
        };

        var stale = program.Revise(0, "Stale", valid, MemberId, "Lead", Now);
        var reversed = program.Revise(1, "Invalid", invalid, MemberId, "Lead", Now);
        var reversedStages = program.Revise(1, "Invalid", reversedJourney, MemberId, "Lead", Now);

        Assert.False(stale.IsSuccess);
        Assert.False(reversed.IsSuccess);
        Assert.False(reversedStages.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error.Kind);
        Assert.Equal(RequestErrorKind.Validation, reversed.Error.Kind);
        Assert.Equal(RequestErrorKind.Validation, reversedStages.Error.Kind);
        Assert.Single(new AggregateScenario<ComplianceProgram>(program).PendingEvents);
    }
}

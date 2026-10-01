using System.Runtime.CompilerServices;
using System.Security.Claims;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Readiness;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.Features.Programs;

public sealed class ProgramStageProjectionTests
{
    static readonly Uuid Runner = Uuid.CreateVersion4();
    static readonly Uuid Decider = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldEnterTypeIOnlyGivenApprovedEntryDecisionAndNotDeferral()
    {
        // Arrange
        await using var fixture = new StoreFixture();
        var tenantId = Uuid.CreateVersion4();
        var approvedProgram = await CreateProgramAsync(fixture, tenantId);
        var deferredProgram = await CreateProgramAsync(fixture, tenantId);
        await DeferAsync(fixture, tenantId, deferredProgram);
        var approvedDecision = await ApproveWithExceptionsAsync(fixture, tenantId,
            approvedProgram);
        using var replay = BuildReplayWorker(fixture.Store, tenantId);

        // Act
        await replay.StartAsync();
        ProgramView? approved = null;
        ProgramView? deferred = null;
        try
        {
            var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
            while (DateTimeOffset.UtcNow < deadline)
            {
                using var scope = replay.Services.CreateScope();
                var directory = scope.ServiceProvider.GetRequiredService<IProgramDirectoryReader>();
                approved = await directory.GetAsync(tenantId, approvedProgram);
                deferred = await directory.GetAsync(tenantId, deferredProgram);
                if (approved?.Stage == "type_i" && deferred is not null)
                    break;
                await Task.Delay(50);
            }
        }
        finally
        {
            await replay.StopAsync();
        }

        // Assert
        Assert.NotNull(approved);
        Assert.Equal("type_i", approved.Stage);
        Assert.Equal("type_ii", approved.NextStage);
        Assert.Equal(approvedDecision, approved.StageDecisionId);
        Assert.Equal(1, approved.Revision);
        Assert.NotNull(approved.StageEnteredBy);
        Assert.NotNull(deferred);
        Assert.Equal("readiness", deferred.Stage);
        Assert.Null(deferred.StageDecisionId);
    }

    static async Task<Uuid> CreateProgramAsync(StoreFixture fixture, Uuid tenantId)
    {
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())],
            "BdgrzSession"));
        var creation = new RequestContext<CreateProgram>(new CreateProgram(tenantId, "SOC 2",
            new ProgramPlan(null, null, null, null, null, null)), actor);
        Assert.True((await new CreateProgramHandler(fixture.Repository, TimeProvider.System)
            .HandleAsync(creation, CancellationToken.None)).IsSuccess);
        return creation.RequestId;
    }

    static async Task<ReadinessAssessmentView> RecordAssessmentAsync(StoreFixture fixture,
        Uuid tenantId, Uuid programId)
    {
        var assessmentId = Uuid.CreateVersion4();
        var asOf = DateTimeOffset.UtcNow.AddMinutes(-1);
        var evaluation = ReadinessRules.Evaluate(programId, asOf, null, [], [],
            new Dictionary<Uuid, ControlVersionView?>());
        var ledger = await ExecuteAsync(fixture, tenantId, programId, item =>
            item.Record(0, assessmentId, asOf, null, evaluation, Runner, "Runner",
                DateTimeOffset.UtcNow));
        return ledger.Read(assessmentId)!;
    }

    static async Task DeferAsync(StoreFixture fixture, Uuid tenantId, Uuid programId)
    {
        var assessment = await RecordAssessmentAsync(fixture, tenantId, programId);
        await ExecuteAsync(fixture, tenantId, programId, item =>
            item.DecideTypeIEntry(assessment.AssessmentId, item.Revision, Uuid.CreateVersion4(),
                ReadinessLedger.Defer, "Not yet.", [], Decider, "Decider",
                DateTimeOffset.UtcNow));
    }

    static async Task<Uuid> ApproveWithExceptionsAsync(StoreFixture fixture, Uuid tenantId,
        Uuid programId)
    {
        var assessment = await RecordAssessmentAsync(fixture, tenantId, programId);
        foreach (var gap in assessment.Gaps)
            await ExecuteAsync(fixture, tenantId, programId, item =>
                item.Plan(gap.GapId, item.Revision, Runner, new DateOnly(2027, 3, 31), "Close.",
                    Runner, "Runner", DateTimeOffset.UtcNow));
        await ExecuteAsync(fixture, tenantId, programId, item =>
            item.Decide(assessment.AssessmentId, item.Revision, Uuid.CreateVersion4(),
                ReadinessLedger.Proceed, "Owned.", Decider, "Decider", DateTimeOffset.UtcNow));
        var decisionId = Uuid.CreateVersion4();
        await ExecuteAsync(fixture, tenantId, programId, item =>
            item.DecideTypeIEntry(assessment.AssessmentId, item.Revision, decisionId,
                ReadinessLedger.ApproveWithExceptions, "Acknowledged.",
                assessment.Gaps.Select(static gap => gap.GapId).ToArray(), Decider, "Decider",
                DateTimeOffset.UtcNow));
        return decisionId;
    }

    static async Task<ReadinessLedger> ExecuteAsync(StoreFixture fixture, Uuid tenantId,
        Uuid programId, Func<ReadinessLedger, CommandFailure?> operation)
    {
        ReadinessLedger? ledger = null;
        var result = await fixture.Repository.ExecuteAsync(new ReadinessLedger(tenantId, programId),
            item =>
            {
                ledger = item;
                var failure = operation(item);
                return AggregateOutcome.Commit(failure is null
                    ? Result.Success
                    : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                        failure.Message!)));
            }, new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsSuccess ? null : result.Error?.Message);
        return ledger!;
    }

    static IHost BuildReplayWorker(IDomainEventReader reader, Uuid tenant)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Development",
        });
        builder.Services.AddSingleton(reader);
        builder.Services.AddSingleton<IKvClient, InMemoryKvClient>();
        builder.Services.AddSingleton<ITenantDirectory>(new StaticTenantDirectory(
            new TenantId(tenant.ToString())));
        builder.Services.AddScoped<FitzProgramDirectory>();
        builder.Services.AddScoped<IProgramDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzProgramDirectory>());
        builder.Services.AddScoped<IProgramDirectoryReader>(provider =>
            provider.GetRequiredService<FitzProgramDirectory>());
        builder.Services.AddPortiaEvent<ProgramCreated>(1, "bdgrz.program.created");
        builder.Services.AddPortiaEvent<ProgramRevised>(1, "bdgrz.program.revised");
        builder.Services.AddPortiaEvent<ProgramCriteriaEditionSelected>(1,
            "bdgrz.program.criteria.selected");
        builder.Services.AddPortiaEvent<TypeIEntryDecisionRecorded>(1,
            "bdgrz.readiness.type_i_entry.decided");
        builder.Services.AddPortia()
            .AddProjector<ProgramDirectoryProjector>("ProgramDirectory", WorkloadScope.PerTenant,
                options => options.PollInterval = TimeSpan.FromMilliseconds(10))
            .UseSingleProcessWorkloads()
            .AddWorkers();
        return builder.Build();
    }

    sealed class StaticTenantDirectory(TenantId tenant) : ITenantDirectory
    {
        public async IAsyncEnumerable<TenantId> GetActiveTenantsAsync(
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            yield return tenant;
            await Task.CompletedTask;
        }

        public async IAsyncEnumerable<TenantLifecycleChange> WatchAsync(
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            yield break;
        }
    }
}

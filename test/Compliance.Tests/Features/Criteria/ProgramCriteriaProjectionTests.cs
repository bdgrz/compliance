using System.Runtime.CompilerServices;
using System.Security.Claims;
using Bdgrz.Compliance.Features.Criteria;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.Features.Criteria;

public sealed class ProgramCriteriaProjectionTests
{
    [Fact]
    public async Task ShouldReplaySelectedEditionIntoProgramViewAndHistoryGivenSourceEvents()
    {
        // Arrange
        await using var fixture = new StoreFixture();
        var tenantId = Uuid.CreateVersion4();
        var editionId = CriteriaCatalog.Platform.Edition.EditionId;
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())],
            "BdgrzSession"));
        var plan = new ProgramPlan(null, null, null, null, null, null);
        var creation = new RequestContext<CreateProgram>(new CreateProgram(tenantId, "SOC 2",
            plan), actor);
        Assert.True((await new CreateProgramHandler(fixture.Repository, TimeProvider.System)
            .HandleAsync(creation, CancellationToken.None)).IsSuccess);
        var programId = creation.RequestId;
        Assert.True((await new SelectProgramCriteriaEditionHandler(fixture.Repository,
                CriteriaCatalog.Platform, TimeProvider.System)
            .HandleAsync(new RequestContext<SelectProgramCriteriaEdition>(
                new SelectProgramCriteriaEdition(tenantId, programId, 1, editionId), actor),
                CancellationToken.None)).IsSuccess);
        Assert.True((await new ReviseProgramHandler(fixture.Repository, TimeProvider.System)
            .HandleAsync(new RequestContext<ReviseProgram>(new ReviseProgram(tenantId, programId,
                2, "SOC 2 revised", plan), actor), CancellationToken.None)).IsSuccess);
        using var replay = BuildReplayWorker(fixture.Store, tenantId);

        // Act
        await replay.StartAsync();
        ProgramView? current = null;
        IReadOnlyList<ProgramRevisionView> history = [];
        try
        {
            var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
            while (DateTimeOffset.UtcNow < deadline)
            {
                using var scope = replay.Services.CreateScope();
                var directory = scope.ServiceProvider.GetRequiredService<IProgramDirectoryReader>();
                current = await directory.GetAsync(tenantId, programId);
                history = (await directory.ListRevisionsAsync(tenantId, programId, 200, null))
                    ?.Items ?? [];
                if (current?.Revision == 3 && history.Count == 3)
                    break;
                await Task.Delay(50);
            }
        }
        finally
        {
            await replay.StopAsync();
        }

        // Assert
        Assert.NotNull(current);
        Assert.Equal(3, current.Revision);
        Assert.Equal("SOC 2 revised", current.Name);
        Assert.Equal(editionId, current.CriteriaEditionId);
        Assert.Collection(history.OrderBy(static revision => revision.Revision),
            revision => Assert.Null(revision.CriteriaEditionId),
            revision => Assert.Equal((2L, editionId),
                (revision.Revision, revision.CriteriaEditionId)),
            revision => Assert.Equal((3L, editionId),
                (revision.Revision, revision.CriteriaEditionId)));
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
        builder.Services.AddPortia()
            .AddProjector<ProgramDirectoryProjector>("ProgramDirectory", WorkloadScope.PerTenant,
                options => options.PollInterval = TimeSpan.FromMilliseconds(10))
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

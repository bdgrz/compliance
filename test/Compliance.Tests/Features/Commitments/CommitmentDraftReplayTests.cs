using System.Security.Claims;
using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Commitments;

public sealed class CommitmentDraftReplayTests
{
    [Fact]
    public async Task ShouldReplayOriginalCreateAndRejectNewDraftGivenRetiredService()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var serviceId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var requestId = Uuid.CreateVersion4();
        var actor = ProgramManagementServices.Actor(actorId);
        await using var provider = ProgramManagementServices.Build(
            new RecordingPermissionAuthorizer(allowed: true),
            portia => portia.AddRequestHandler<CreateCommitmentDraftHandler>());
        await ProgramManagementServices.SeedAsync(provider, new ClientService(tenantId, serviceId),
            service => service.Create(programId, "Client service", "Purpose", "Operations",
                actorId, "Author", DateTimeOffset.UtcNow));
        var original = new CreateCommitmentDraft(tenantId, programId, serviceId,
            "service_commitment", "SC-01", "Statement", "Context", "Source A");
        var registration = new CommitmentDraftRegistration(CommitmentDraft.IdFor(tenantId,
            programId, "service_commitment", "SC-01"), "service_commitment", "SC-01", 1);
        await Dispatch(provider, actor, original, requestId).ExpectSuccess(registration);
        await ProgramManagementServices.SeedAsync(provider, new ClientService(tenantId, serviceId),
            service => service.Retire(1, "Service ended", actorId, "Author",
                DateTimeOffset.UtcNow));

        // Act
        // Assert
        await Dispatch(provider, actor, original, requestId).ExpectSuccess(registration);
        await Dispatch(provider, actor, original with { Identifier = "SC-02" },
            Uuid.CreateVersion4()).ExpectFailure(RequestErrorKind.NotFound);
        await Dispatch(provider, actor, original with { Statement = "Changed statement" },
            requestId).ExpectFailure(RequestErrorKind.Conflict);
        var draft = await ProgramManagementServices.HydrateAsync(provider,
            new CommitmentDraft(tenantId, registration.DraftId));
        var rejectedDraft = await ProgramManagementServices.HydrateAsync(provider,
            new CommitmentDraft(tenantId,
                CommitmentDraft.IdFor(tenantId, programId, "service_commitment", "SC-02")));
        Assert.Equal(1, draft.Revision);
        Assert.False(rejectedDraft.IsCreated);
    }

    static RequestExpectations<CommitmentDraftRegistration> Dispatch(IServiceProvider provider,
        ClaimsPrincipal actor, CreateCommitmentDraft request,
        Uuid requestId) => RequestScenario.For(provider)
        .GivenActor(actor)
        .GivenMetadata(new RequestMetadata(requestId, requestId, null))
        .When(request)
        .ExpectAuthorized()
        .ExpectHandled();
}

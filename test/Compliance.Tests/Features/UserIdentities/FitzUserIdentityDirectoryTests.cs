using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.UserIdentities;

public sealed class FitzUserIdentityDirectoryTests
{
    [Fact]
    public async Task ShouldIncrementProjectionRevisionForEverySourceEventGivenIdentityChanges()
    {
        // Arrange
        var directory = new FitzUserIdentityDirectory(new InMemoryKvClient());
        var userId = Uuid.CreateVersion4();
        const string provider = "https://issuer.example/";
        const string identifier = "subject";
        const string replacementProvider = "https://replacement.example/";
        const string replacementIdentifier = "replacement";
        var identityId = UserIdentity.GetIdentityId(provider, identifier);
        var replacementId = UserIdentity.GetIdentityId(replacementProvider, replacementIdentifier);
        var registered = new UserIdentityRegistered(userId, provider, identifier, null);
        var authenticated = new UserIdentityAuthenticated(userId, provider, identifier);
        var replacementRegistered = new UserIdentityRegistered(userId, replacementProvider,
            replacementIdentifier, null);
        var revoked = new UserIdentityRevoked(identityId, userId, replacementId,
            DateTimeOffset.UtcNow);
        var source = EventStreamPattern.ForPattern("bdgrz", "user-identities");
        var checkpoint = new CheckpointIdentity("UserIdentityDirectory", source);

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         checkpoint, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(registered);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var afterRegistration = await directory.ListAsync(userId, 50, null);
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         checkpoint, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(authenticated);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var afterAuthentication = await directory.ListAsync(userId, 50, null);
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         checkpoint, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(replacementRegistered);
            await directory.ApplyAsync(revoked);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var afterRevocation = await directory.ListAsync(userId, 50, null);

        // Assert
        Assert.Equal(1, afterRegistration.Revision);
        Assert.Equal(2, afterAuthentication.Revision);
        Assert.Equal(4, afterRevocation.Revision);
    }

    [Fact]
    public async Task ShouldReportLagGivenIdentityDirectoryCheckpointBehindSource()
    {
        // Arrange
        var directory = new FitzUserIdentityDirectory(new InMemoryKvClient());
        var events = new InMemoryEventStore();
        var consistency = new IdentityDirectoryReadConsistency(directory, events);
        var userId = Uuid.CreateVersion4();
        var registered = new UserIdentityRegistered(userId,
            "https://issuer.example/", "subject", null);
        var identityId = UserIdentity.GetIdentityId(registered.Provider, registered.Identifier);
        var stream = new EventStreamAddress("bdgrz", "user-identities", identityId.ToString());
        registered.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), identityId, 1,
            DateTimeOffset.UtcNow));

        // Act
        var caughtUpWhenEmpty = await consistency.EnsureCaughtUpAsync(CancellationToken.None);
        await events.AppendAsync(stream, 0, [registered]);
        var lagged = await consistency.EnsureCaughtUpAsync(CancellationToken.None);
        await using var source = events.ReadAsync(EventStreamPattern.ForPattern("bdgrz", "user-identities"),
            EventCursor.Start, CancellationToken.None).GetAsyncEnumerator();
        Assert.True(await source.MoveNextAsync());
        var sourceCursor = source.Current.NextCursor;
        var checkpoint = new CheckpointIdentity("UserIdentityDirectory",
            EventStreamPattern.ForPattern("bdgrz", "user-identities"));
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         checkpoint, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(registered);
            await batch.CommitAsync(new ProjectionCheckpoint(sourceCursor));
        }
        var caughtUpAfterProjection = await consistency.EnsureCaughtUpAsync(CancellationToken.None);

        // Assert
        Assert.True(caughtUpWhenEmpty.IsSuccess);
        Assert.False(lagged.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, lagged.Error.Kind);
        Assert.True(lagged.Error.IsTransient);
        Assert.True(caughtUpAfterProjection.IsSuccess);
    }

    [Fact]
    public async Task ShouldListOnlyActiveIdentitiesGivenRegistrationAndRevocation()
    {
        // Arrange
        var directory = new FitzUserIdentityDirectory(new InMemoryKvClient());
        var owner = Uuid.CreateVersion4();
        var other = Uuid.CreateVersion4();
        var old = new UserIdentity("https://old-issuer.example/", "old-subject");
        var current = new UserIdentity("https://current-issuer.example/", "current-subject");
        var others = new UserIdentity("https://other-issuer.example/", "other-subject");
        var source = EventStreamPattern.ForPattern("bdgrz", "user-identities");
        var checkpoint = new CheckpointIdentity("UserIdentityDirectory", source);
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(checkpoint, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new UserIdentityRegistered(owner,
                "https://old-issuer.example/", "old-subject", "owner@example.com"));
            await directory.ApplyAsync(new UserIdentityRegistered(owner,
                "https://current-issuer.example/", "current-subject", null));
            await directory.ApplyAsync(new UserIdentityRegistered(other,
                "https://other-issuer.example/", "other-subject", null));
            await directory.ApplyAsync(new UserIdentityRevoked(old.Id, owner,
                current.Id, DateTimeOffset.UtcNow));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var page = await directory.ListAsync(owner, 50, null);

        // Assert
        var identity = Assert.Single(page.Identities.Items);
        Assert.Equal(current.Id, identity.UserIdentityId);
        Assert.Equal("https://current-issuer.example/", identity.Provider);
        Assert.False(identity.IsRevoked);
        Assert.Equal(4, page.Revision);
        Assert.DoesNotContain(page.Identities.Items, item => item.UserIdentityId == old.Id ||
            item.UserIdentityId == others.Id);
    }
}

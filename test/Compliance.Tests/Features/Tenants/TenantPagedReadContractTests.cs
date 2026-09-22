using System.Security.Claims;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class TenantPagedReadContractTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public async Task ShouldRejectOutOfRangeLimitGivenTenantPagedReads(int limit)
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var tenants = new TenantDirectory();
        var activeTenants = new ActiveTenantDirectory();
        var memberships = new MembershipDirectory();
        var invitations = new InvitationDirectory();
        var actor = Actor();

        // Act
        var platform = await new ListTenantsHandler(tenants).HandleAsync(
            Context(new ListTenants(limit), actor), CancellationToken.None);
        var mine = await new ListMyTenantsHandler(activeTenants, memberships, tenants).HandleAsync(
            Context(new ListMyTenants(limit), actor), CancellationToken.None);
        var members = await new ListTenantMembersHandler(memberships).HandleAsync(
            Context(new ListTenantMembers(tenantId, limit), actor), CancellationToken.None);
        var invitationPage = await new ListTenantInvitationsHandler(invitations, memberships,
            TimeProvider.System).HandleAsync(
            Context(new ListTenantInvitations(tenantId, limit), actor), CancellationToken.None);

        // Assert
        AssertValidation(platform.Error);
        AssertValidation(mine.Error);
        AssertValidation(members.Error);
        AssertValidation(invitationPage.Error);
        Assert.Equal(0, tenants.ListCount);
        Assert.Equal(0, activeTenants.EnumerationCount);
        Assert.Equal(0, memberships.ListCount);
        Assert.Equal(0, invitations.ListCount);
    }

    [Fact]
    public async Task ShouldUseDefaultLimitGivenOmittedTenantPagedReads()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var tenants = new TenantDirectory();
        var memberships = new MembershipDirectory();
        var invitations = new InvitationDirectory();
        var actor = Actor();

        // Act
        _ = await new ListTenantsHandler(tenants).HandleAsync(
            Context(new ListTenants(), actor), CancellationToken.None);
        _ = await new ListTenantMembersHandler(memberships).HandleAsync(
            Context(new ListTenantMembers(tenantId), actor), CancellationToken.None);
        _ = await new ListTenantInvitationsHandler(invitations, memberships, TimeProvider.System)
            .HandleAsync(Context(new ListTenantInvitations(tenantId), actor), CancellationToken.None);

        // Assert
        Assert.Equal(50, tenants.LastLimit);
        Assert.Equal(50, memberships.LastLimit);
        Assert.Equal(50, invitations.LastLimit);
    }

    [Fact]
    public async Task ShouldRejectInvalidCursorGivenTenantPagedReads()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var tenants = new TenantDirectory { RejectCursor = true };
        var activeTenants = new ActiveTenantDirectory();
        var memberships = new MembershipDirectory { RejectCursor = true };
        var invitations = new InvitationDirectory { RejectCursor = true };
        var actor = Actor();

        // Act
        var platform = await new ListTenantsHandler(tenants).HandleAsync(
            Context(new ListTenants(Cursor: "invalid"), actor), CancellationToken.None);
        var mine = await new ListMyTenantsHandler(activeTenants, memberships, tenants).HandleAsync(
            Context(new ListMyTenants(Cursor: "invalid"), actor), CancellationToken.None);
        var members = await new ListTenantMembersHandler(memberships).HandleAsync(
            Context(new ListTenantMembers(tenantId, Cursor: "invalid"), actor), CancellationToken.None);
        var invitationPage = await new ListTenantInvitationsHandler(invitations, memberships,
            TimeProvider.System).HandleAsync(
            Context(new ListTenantInvitations(tenantId, Cursor: "invalid"), actor), CancellationToken.None);

        // Assert
        AssertValidation(platform.Error);
        AssertValidation(mine.Error);
        AssertValidation(members.Error);
        AssertValidation(invitationPage.Error);
        Assert.Equal(0, activeTenants.EnumerationCount);
    }

    [Fact]
    public async Task ShouldRejectCursorGivenFilteredTenantInvitationList()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var invitations = new InvitationDirectory { Entry = Invitation(tenantId) };
        var handler = new ListTenantInvitationsHandler(invitations, new MembershipDirectory(),
            TimeProvider.System);

        // Act
        var result = await handler.HandleAsync(Context(new ListTenantInvitations(tenantId,
            Cursor: "unused", EmailAddress: "invitee@example.com"), Actor()), CancellationToken.None);

        // Assert
        AssertValidation(result.Error);
        Assert.Equal(0, invitations.GetCount);
        Assert.Equal(0, invitations.ListCount);
    }

    [Fact]
    public async Task ShouldNormalizeUuidCursorGivenOwnTenantPaging()
    {
        // Arrange
        var tenantIds = new[] { Uuid.CreateVersion4(), Uuid.CreateVersion4() }
            .OrderBy(id => id.ToString(), StringComparer.Ordinal).ToArray();
        var tenants = new TenantDirectory();
        tenants.ById[tenantIds[0]] = new TenantView(tenantIds[0], "First", "first");
        tenants.ById[tenantIds[1]] = new TenantView(tenantIds[1], "Second", "second");
        var activeTenants = new ActiveTenantDirectory(tenantIds);
        var memberships = new MembershipDirectory { IsMember = true };
        var handler = new ListMyTenantsHandler(activeTenants, memberships, tenants);

        // Act
        var result = await handler.HandleAsync(Context(new ListMyTenants(Limit: 1,
            Cursor: tenantIds[0].ToString().ToUpperInvariant()), Actor()), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(tenantIds[1], Assert.Single(result.Value.Items).TenantId);
    }

    [Fact]
    public async Task ShouldHideMismatchedTenantProjectionGivenTenantScopedPagedReads()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var otherTenantId = Uuid.CreateVersion4();
        var tenants = new TenantDirectory();
        tenants.ById[tenantId] = new TenantView(otherTenantId, "Other", "other");
        var activeTenants = new ActiveTenantDirectory(tenantId);
        var memberships = new MembershipDirectory
        {
            IsMember = true,
            Page = new Page<TenantMembershipView>([
                new TenantMembershipView(Uuid.CreateVersion4(), otherTenantId),
            ], null),
        };
        var entry = Invitation(otherTenantId);
        var invitations = new InvitationDirectory
        {
            Entry = entry,
            Page = new Page<TenantInvitationDirectoryEntry>([entry], null),
        };
        var actor = Actor();

        // Act
        var mine = await new ListMyTenantsHandler(activeTenants, memberships, tenants).HandleAsync(
            Context(new ListMyTenants(), actor), CancellationToken.None);
        var members = await new ListTenantMembersHandler(memberships).HandleAsync(
            Context(new ListTenantMembers(tenantId), actor), CancellationToken.None);
        var invitationPage = await new ListTenantInvitationsHandler(invitations, memberships,
            TimeProvider.System).HandleAsync(
            Context(new ListTenantInvitations(tenantId), actor), CancellationToken.None);
        var invitationFilter = await new ListTenantInvitationsHandler(invitations, memberships,
            TimeProvider.System).HandleAsync(Context(new ListTenantInvitations(tenantId,
                EmailAddress: entry.EmailAddress), actor), CancellationToken.None);

        // Assert
        AssertNotFound(mine.Error);
        AssertNotFound(members.Error);
        AssertNotFound(invitationPage.Error);
        AssertNotFound(invitationFilter.Error);
    }

    static TenantInvitationDirectoryEntry Invitation(Uuid tenantId) => new(tenantId,
        "invitee@example.com", "client_personnel", false, null, DateTimeOffset.UtcNow.AddDays(1),
        Uuid.CreateVersion4(), null);

    static ClaimsPrincipal Actor() => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())],
        "BdgrzSession"));

    static RequestContext<TRequest> Context<TRequest>(TRequest request, ClaimsPrincipal actor)
        where TRequest : IRequestBase => new(request, actor);

    static void AssertValidation(RequestError? error) =>
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(error).Kind);

    static void AssertNotFound(RequestError? error) =>
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(error).Kind);

    sealed class TenantDirectory : ITenantDirectoryReader
    {
        public Dictionary<Uuid, TenantView> ById { get; } = [];
        public int LastLimit { get; private set; }
        public int ListCount { get; private set; }
        public bool RejectCursor { get; init; }

        public ValueTask<TenantView?> GetAsync(Uuid tenantId, CancellationToken ct = default) =>
            ValueTask.FromResult(ById.TryGetValue(tenantId, out var tenant) ? tenant : null);

        public ValueTask<Page<TenantView>> ListAsync(int limit, string? cursor,
            CancellationToken ct = default)
        {
            ListCount++;
            LastLimit = limit;
            return RejectCursor
                ? ValueTask.FromException<Page<TenantView>>(new KvDirectoryQueryException())
                : ValueTask.FromResult(new Page<TenantView>([], null));
        }
    }

    sealed class ActiveTenantDirectory(params Uuid[] tenantIds) : ITenantDirectory
    {
        public int EnumerationCount { get; private set; }

        public async IAsyncEnumerable<TenantId> GetActiveTenantsAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            EnumerationCount++;
            foreach (var tenantId in tenantIds)
            {
                yield return new TenantId(tenantId.ToString());
            }

            await Task.CompletedTask;
        }

        public async IAsyncEnumerable<TenantLifecycleChange> WatchAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    sealed class MembershipDirectory : ITenantMembershipDirectoryReader
    {
        public bool IsMember { get; init; }
        public int LastLimit { get; private set; }
        public int ListCount { get; private set; }
        public Page<TenantMembershipView> Page { get; init; } = new([], null);
        public bool RejectCursor { get; init; }

        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId,
            CancellationToken ct = default) => ValueTask.FromResult(IsMember);

        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit,
            string? cursor, CancellationToken ct = default)
        {
            ListCount++;
            LastLimit = limit;
            return RejectCursor
                ? ValueTask.FromException<Page<TenantMembershipView>>(new KvDirectoryQueryException())
                : ValueTask.FromResult(Page);
        }
    }

    sealed class InvitationDirectory : ITenantInvitationDirectoryReader
    {
        public TenantInvitationDirectoryEntry? Entry { get; init; }
        public int GetCount { get; private set; }
        public int LastLimit { get; private set; }
        public int ListCount { get; private set; }
        public Page<TenantInvitationDirectoryEntry> Page { get; init; } = new([], null);
        public bool RejectCursor { get; init; }

        public ValueTask<TenantInvitationDirectoryEntry?> GetAsync(Uuid tenantId,
            string emailAddress, CancellationToken ct = default)
        {
            GetCount++;
            return ValueTask.FromResult(Entry);
        }

        public ValueTask<Page<TenantInvitationDirectoryEntry>> ListAsync(Uuid tenantId, int limit,
            string? cursor, CancellationToken ct = default)
        {
            ListCount++;
            LastLimit = limit;
            return RejectCursor
                ? ValueTask.FromException<Page<TenantInvitationDirectoryEntry>>(
                    new KvDirectoryQueryException())
                : ValueTask.FromResult(Page);
        }
    }
}

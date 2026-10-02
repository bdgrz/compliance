using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class MemberEmailEnrichmentTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid VerifiedUser = Uuid.CreateVersion4();
    static readonly Uuid UnverifiedUser = Uuid.CreateVersion4();

    [Theory]
    [InlineData("Roster name", "Provider name", "Roster name")]
    [InlineData(null, "Provider name", "Provider name")]
    [InlineData(null, null, "a.lead@example.com")]
    public async Task ShouldUsePresentationPrecedenceGivenExplicitAndAuthenticatedNames(
        string? personName, string? profileName, string expected)
    {
        // Arrange
        var names = new Names(personName, profileName);
        var handler = new GetTenantMemberHandler(new Members(), Emails(), names, names);
        var context = new RequestContext<GetTenantMember>(new GetTenantMember(TenantId, VerifiedUser), Actor());

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(expected, result.Value.DisplayName);
        Assert.Equal("a.lead@example.com", result.Value.VerifiedEmailAddress);
    }

    [Fact]
    public async Task ShouldUseVerifiedEmailThenUserIdGivenNoCorrelatedOrProviderName()
    {
        // Arrange
        var handler = new ListTenantMembersHandler(new Members(), Emails());
        var context = new RequestContext<ListTenantMembers>(new ListTenantMembers(TenantId), Actor());

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        var byUser = result.Value.Items.ToDictionary(member => member.UserId);
        Assert.Equal("a.lead@example.com", byUser[VerifiedUser].DisplayName);
        Assert.Equal(UnverifiedUser.ToString(), byUser[UnverifiedUser].DisplayName);
    }

    [Fact]
    public async Task ShouldFindVerifiedEmailGivenUnverifiedAddressesFillFirstPage()
    {
        // Arrange
        var addresses = Enumerable.Range(0, 20).Select(index =>
            new EmailAddressView(VerifiedUser, $"pending-{index}@example.com", false))
            .Append(new EmailAddressView(VerifiedUser, "verified@example.com", true)).ToArray();
        var handler = new GetTenantMemberHandler(new Members(), new EmailDirectory(
            new Dictionary<Uuid, EmailAddressView[]> { [VerifiedUser] = addresses }));
        var context = new RequestContext<GetTenantMember>(new GetTenantMember(TenantId, VerifiedUser), Actor());

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal("verified@example.com", result.Value.VerifiedEmailAddress);
    }

    [Fact]
    public async Task ShouldShowOnlyVerifiedEmailGivenMemberList()
    {
        // Arrange
        var handler = new ListTenantMembersHandler(new Members(), Emails());
        var context = new RequestContext<ListTenantMembers>(new ListTenantMembers(TenantId), Actor());

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var byUser = result.Value.Items.ToDictionary(member => member.UserId);
        Assert.Equal("a.lead@example.com", byUser[VerifiedUser].VerifiedEmailAddress);
        Assert.Null(byUser[UnverifiedUser].VerifiedEmailAddress);
    }

    [Fact]
    public async Task ShouldShowVerifiedEmailGivenSingleMemberRead()
    {
        // Arrange
        var handler = new GetTenantMemberHandler(new Members(), Emails());
        var context = new RequestContext<GetTenantMember>(new GetTenantMember(TenantId, VerifiedUser), Actor());

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal("a.lead@example.com", result.Value.VerifiedEmailAddress);
    }

    static EmailDirectory Emails() => new(new Dictionary<Uuid, EmailAddressView[]>
    {
        [VerifiedUser] =
        [
            new(VerifiedUser, "z.personal@example.com", true),
            new(VerifiedUser, "pending@example.com", false),
            new(VerifiedUser, "a.lead@example.com", true),
        ],
        [UnverifiedUser] = [new(UnverifiedUser, "unconfirmed@example.com", false)],
    });

    static ClaimsPrincipal Actor() => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())], "BdgrzSession"));

    sealed class Members : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId,
            CancellationToken ct = default) =>
            ValueTask.FromResult<TenantMembershipView?>(new TenantMembershipView(userId, TenantId));

        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(true);

        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor,
            CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TenantMembershipView>(
                [new(VerifiedUser, TenantId), new(UnverifiedUser, TenantId)], null));
    }

    sealed class Names(string? personName, string? profileName)
        : IUserDisplayNameReader, IPersonMemberDisplayReader
    {
        public ValueTask<string?> ReadAsync(Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(userId == VerifiedUser ? profileName : null);

        public ValueTask<string?> ReadAsync(Uuid tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(tenantId == TenantId && userId == VerifiedUser ? personName : null);
    }

    sealed class EmailDirectory(Dictionary<Uuid, EmailAddressView[]> byUser) : IEmailAddressDirectoryReader
    {
        public ValueTask<EmailAddressView?> GetAsync(string emailAddress, CancellationToken ct = default) =>
            ValueTask.FromResult<EmailAddressView?>(null);

        public ValueTask<Page<EmailAddressView>> ListAsync(Uuid userId, int? limit, string? cursor,
            CancellationToken ct = default)
        {
            var items = byUser.GetValueOrDefault(userId) ?? [];
            var offset = cursor is null ? 0 : int.Parse(cursor, System.Globalization.CultureInfo.InvariantCulture);
            var size = limit ?? 20;
            return ValueTask.FromResult(new Page<EmailAddressView>(items.Skip(offset).Take(size).ToArray(),
                offset + size < items.Length
                    ? (offset + size).ToString(System.Globalization.CultureInfo.InvariantCulture) : null));
        }
    }
}

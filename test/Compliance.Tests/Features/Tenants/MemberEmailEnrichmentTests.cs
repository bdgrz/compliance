using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.UserIdentities;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class MemberEmailEnrichmentTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid VerifiedUser = Uuid.CreateVersion4();
    static readonly Uuid UnverifiedUser = Uuid.CreateVersion4();

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

    sealed class EmailDirectory(Dictionary<Uuid, EmailAddressView[]> byUser) : IEmailAddressDirectoryReader
    {
        public ValueTask<EmailAddressView?> GetAsync(string emailAddress, CancellationToken ct = default) =>
            ValueTask.FromResult<EmailAddressView?>(null);

        public ValueTask<Page<EmailAddressView>> ListAsync(Uuid userId, int? limit, string? cursor,
            CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<EmailAddressView>(byUser.GetValueOrDefault(userId) ?? [], null));
    }
}

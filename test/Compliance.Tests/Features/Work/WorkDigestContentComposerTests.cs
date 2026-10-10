using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Work;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class WorkDigestContentComposerTests
{
    [Fact]
    public void ShouldSelectAssignedVisibleWorkWithinFrozenLocalWindowGivenMultiplePrograms()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var memberId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var otherProgramId = Uuid.CreateVersion4();
        var weekOf = new DateOnly(2026, 10, 5);
        var settings = new WorkDigestDeliverySettings("https://app.example",
            "/tenants/{tenant_id}/{tenant_slug}/programs/{program_id}/work/{work_item_id}", 3,
            TimeSpan.FromMinutes(5), TimeSpan.FromDays(6), TimeSpan.FromMinutes(3),
            TimeSpan.FromMinutes(1));
        var overdue = Item(Uuid.CreateVersion4(), memberId, weekOf.AddDays(-1), "/api/secret/source");
        var firstDueSoon = Item(Uuid.CreateVersion4(), memberId, weekOf, "/api/secret/source");
        var lastDueSoon = Item(Uuid.CreateVersion4(), memberId, weekOf.AddDays(7), "/api/secret/source");
        var requiresWaiver = Item(Uuid.CreateVersion4(), memberId, weekOf.AddDays(2),
            "/api/secret/source", requiresWaiver: true);
        var outsideWindow = Item(Uuid.CreateVersion4(), memberId, weekOf.AddDays(8), "/api/secret/source");
        var otherProgramItem = Item(Uuid.CreateVersion4(), memberId, weekOf.AddDays(3),
            "/api/secret/source");
        var anotherMember = Item(Uuid.CreateVersion4(), Uuid.CreateVersion4(), weekOf,
            "/api/secret/source");
        var unassignedOversight = Item(Uuid.CreateVersion4(), Uuid.CreateVersion4(), weekOf,
            "/api/secret/source") with
        { AssigneeMemberId = null };
        var visibleItems = new[]
        {
            new WorkDigestSourceItem(programId, "Security", overdue),
            new WorkDigestSourceItem(programId, "Security", firstDueSoon),
            new WorkDigestSourceItem(programId, "Security", lastDueSoon),
            new WorkDigestSourceItem(programId, "Security", requiresWaiver),
            new WorkDigestSourceItem(programId, "Security", outsideWindow),
            new WorkDigestSourceItem(otherProgramId, "Privacy", otherProgramItem),
            new WorkDigestSourceItem(otherProgramId, "Privacy", anotherMember),
            new WorkDigestSourceItem(otherProgramId, "Privacy", unassignedOversight),
            new WorkDigestSourceItem(programId, "Security", firstDueSoon),
        };

        // Act
        var message = WorkDigestContentComposer.Compose(tenantId, memberId, weekOf, "acme",
            Uuid.CreateVersion5(tenantId, $"{memberId}:digest:{weekOf:yyyy-MM-dd}"),
            "member@example.com", visibleItems, settings);

        // Assert
        Assert.NotNull(message);
        Assert.Equal("member@example.com", message!.Recipient);
        Assert.Contains("Security", message.TextBody, StringComparison.Ordinal);
        Assert.Contains("Privacy", message.TextBody, StringComparison.Ordinal);
        Assert.Contains(firstDueSoon.Summary, message.TextBody, StringComparison.Ordinal);
        Assert.Contains(lastDueSoon.Summary, message.TextBody, StringComparison.Ordinal);
        Assert.Contains(requiresWaiver.Summary, message.TextBody, StringComparison.Ordinal);
        Assert.Contains(overdue.Summary, message.TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain(outsideWindow.Summary, message.TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain(anotherMember.Summary, message.TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain(unassignedOversight.Summary, message.TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/secret/source", message.TextBody, StringComparison.Ordinal);
        Assert.Equal(1, Count(message.TextBody, firstDueSoon.Summary));
        var firstDueSoonLink = $"https://app.example/tenants/{tenantId}/acme/programs/" +
                               $"{programId}/work/{firstDueSoon.WorkItemId}";
        Assert.Contains(firstDueSoonLink, message.TextBody, StringComparison.Ordinal);
    }

    static WorkQueueItemView Item(Uuid workItemId, Uuid assignee, DateOnly dueOn, string actionPath,
        bool requiresWaiver = false) =>
        new(workItemId, "evidence_request", Uuid.CreateVersion4(), null, null,
            $"Item {workItemId}", "Sensitive source instructions", dueOn, false, null,
            "fulfil", actionPath, new OperatingHolder("member", assignee), assignee,
            0, false, null, DateTimeOffset.UnixEpoch)
        {
            RequiresSeparationOfDutiesWaiver = requiresWaiver,
        };

    static int Count(string value, string fragment)
    {
        var count = 0;
        var offset = 0;
        while ((offset = value.IndexOf(fragment, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += fragment.Length;
        }
        return count;
    }
}

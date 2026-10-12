using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

sealed record ActorVisibleApplicationRelationships(long Revision,
    IReadOnlyList<ApplicationRelationshipView> ActiveItems, string Fingerprint);

sealed record ActorVisibleApplicationRelationshipPreview(
    IReadOnlyList<ApplicationRelationshipView> Items, bool HasMore);

static class ApplicationRelationshipProjection
{
    const int MaximumPreviewItems = 200;

    public static async ValueTask<ActorVisibleApplicationRelationships> ReadAsync(
        DeclaredApplication source, Uuid tenantId, Uuid userId,
        RestrictedApplicationVisibility visibility, CancellationToken ct)
    {
        var readableTargets = new Dictionary<Uuid, bool>();
        var activeItems = new List<ApplicationRelationshipView>();
        long revision = 0;
        foreach (var relationship in source.ReadApplicationRelationshipHistory())
        {
            var targetId = relationship.View.TargetApplicationId;
            if (!readableTargets.TryGetValue(targetId, out var canRead))
            {
                canRead = await visibility.CanReadApplicationAsync(tenantId, userId,
                    targetId, ct).ConfigureAwait(false);
                readableTargets.Add(targetId, canRead);
            }
            if (!canRead)
                continue;

            revision += relationship.EventCount;
            if (relationship.IsActive)
                activeItems.Add(relationship.View);
        }

        return new ActorVisibleApplicationRelationships(revision, activeItems,
            Fingerprint(activeItems));
    }

    public static async ValueTask<ActorVisibleApplicationRelationshipPreview> ReadPreviewAsync(
        DeclaredApplication source, Uuid tenantId, Uuid userId,
        RestrictedApplicationVisibility visibility, CancellationToken ct)
    {
        var activeItems = new List<ApplicationRelationshipView>(MaximumPreviewItems + 1);
        foreach (var relationship in source.EnumerateApplicationRelationshipHistory())
        {
            if (!relationship.IsActive || !await visibility.CanReadApplicationAsync(tenantId,
                    userId, relationship.View.TargetApplicationId, ct).ConfigureAwait(false))
                continue;

            activeItems.Add(relationship.View);
            if (activeItems.Count > MaximumPreviewItems)
                break;
        }

        return new ActorVisibleApplicationRelationshipPreview(
            activeItems.Take(MaximumPreviewItems).ToArray(),
            activeItems.Count > MaximumPreviewItems);
    }

    static string Fingerprint(IReadOnlyList<ApplicationRelationshipView> items)
    {
        var rows = items.Select(static item => new[]
        {
            item.RelationshipId.ToString(),
            item.TargetApplicationId.ToString(),
            item.RelationshipType,
            item.ActorMemberId.ToString(),
            item.ActorDisplay,
            item.ChangedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        });
        var payload = JsonSerializer.Serialize(rows);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }
}

static class ApplicationRelationshipConcurrency
{
    const string SourceChangedMessage =
        "The application relationship source changed during this request; retry.";

    public static RequestError SourceChanged() => new(RequestErrorKind.Conflict,
        SourceChangedMessage, isTransient: true);

    public static bool IsSourceChanged(RequestError? error) =>
        error is { Kind: RequestErrorKind.Conflict, IsTransient: true } &&
        error.Message == SourceChangedMessage;

    public static RequestError VisibleRevisionMismatch(long expected, long actual) =>
        new(RequestErrorKind.Conflict,
            $"Expected relationship revision {expected} did not match current " +
            $"actor-visible revision {actual}; refresh and retry.", isTransient: true);
}

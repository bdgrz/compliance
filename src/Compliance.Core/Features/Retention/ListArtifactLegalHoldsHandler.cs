using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

sealed class ListArtifactLegalHoldsHandler(IAggregateReader reader)
    : IRequestHandler<ListArtifactLegalHolds, Page<ArtifactLegalHoldView>>
{
    public async ValueTask<Result<Page<ArtifactLegalHoldView>>> HandleAsync(
        IRequestContext<ListArtifactLegalHolds> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200 || request.MinimumRevision is < 1 || request.Cursor is { Length: > 256 })
            return Fail(RequestErrorKind.Validation, "The retention history paging parameters are invalid.");
        var source = await ArtifactRetentionSourceReader.LoadAsync(reader, request.TenantId,
            request.SourceKind, request.SourceId, ct).ConfigureAwait(false);
        if (!source.IsSuccess)
            return Result<Page<ArtifactLegalHoldView>>.Failure(source.Error);
        var retention = await reader.HydrateAsync(new ArtifactRetention(request.TenantId,
            request.SourceKind, request.SourceId), ct).ConfigureAwait(false);
        var valid = retention.ValidateSource(source.Value);
        if (!valid.IsSuccess)
            return Result<Page<ArtifactLegalHoldView>>.Failure(valid.Error);
        if (request.MinimumRevision > retention.Revision)
            return Fail(RequestErrorKind.Conflict, "The requested retention revision is not yet durable.", true);
        var prefix = string.Create(CultureInfo.InvariantCulture,
            $"retention_holds_v1:{request.TenantId}:{request.SourceKind}:{request.SourceId}:{retention.Revision}:{source.Value.Position}:");
        var offset = 0;
        if (request.Cursor is { } cursor)
        {
            var parts = cursor.Split(':');
            if (parts.Length != 7 || parts[0] != "retention_holds_v1" ||
                parts[1] != request.TenantId.ToString() || parts[2] != request.SourceKind ||
                parts[3] != request.SourceId.ToString() ||
                !long.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out var revision) ||
                !ulong.TryParse(parts[5], NumberStyles.None, CultureInfo.InvariantCulture, out var position) ||
                !int.TryParse(parts[6], NumberStyles.None, CultureInfo.InvariantCulture, out offset))
                return Fail(RequestErrorKind.Validation, "The retention history cursor is invalid.");
            if (revision != retention.Revision || position != source.Value.Position)
                return Fail(RequestErrorKind.Conflict, "The retention history cursor is stale.", true);
        }
        var holds = retention.GetLegalHolds().OrderBy(hold => hold.HoldId.ToString(), StringComparer.Ordinal).ToArray();
        if (offset > holds.Length)
            return Fail(RequestErrorKind.Validation, "The retention history cursor is invalid.");
        var items = Array.AsReadOnly(holds.Skip(offset).Take(request.Limit ?? 50).ToArray());
        var next = offset + items.Count < holds.Length ? prefix + (offset + items.Count).ToString(CultureInfo.InvariantCulture) : null;
        var fence = await ArtifactRetentionSourceReader.CheckAsync(reader, source.Value, ct).ConfigureAwait(false);
        if (!fence.IsSuccess)
            return Result<Page<ArtifactLegalHoldView>>.Failure(fence.Error);
        var current = await reader.HydrateAsync(new ArtifactRetention(request.TenantId,
            request.SourceKind, request.SourceId), ct).ConfigureAwait(false);
        if (current.CommittedStreamPosition != retention.CommittedStreamPosition)
            return Fail(RequestErrorKind.Conflict, "Retention changed during this request.", true);
        return Result<Page<ArtifactLegalHoldView>>.Success(new(items, next));
    }

    static Result<Page<ArtifactLegalHoldView>> Fail(RequestErrorKind kind, string message, bool transient = false) =>
        Result<Page<ArtifactLegalHoldView>>.Failure(new RequestError(kind, message, isTransient: transient));
}

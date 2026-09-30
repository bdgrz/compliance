using System.Globalization;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>
///     Serves approved-version and decision reads from the control's own event stream. A control
///     holds at most one initial version and a bounded decision list, so source reads are exact,
///     never lag, and keep replay as the only recovery step.
/// </summary>
public sealed class ControlActivationSource(IAggregateReader reader)
{
    public const int MaximumPageSize = 200;

    public async ValueTask<Result<ControlDraft>> LoadAsync(Uuid tenantId, Uuid programId,
        Uuid controlId, CancellationToken ct)
    {
        var control = await reader.HydrateAsync(new ControlDraft(tenantId, controlId), ct)
            .ConfigureAwait(false);
        return control.IsVisible && control.ProgramId == programId
            ? Result<ControlDraft>.Success(control)
            : Result<ControlDraft>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The control was not found."));
    }

    public static Result<ControlVersionView> Version(ControlVersionView? version, string missing) =>
        version is not null
            ? Result<ControlVersionView>.Success(version)
            : Result<ControlVersionView>.Failure(new RequestError(RequestErrorKind.NotFound,
                missing));

    public static Result<Page<T>> Paginate<T>(IReadOnlyList<T> items, int? limit, string? cursor,
        string record)
    {
        if (limit is < 1 or > MaximumPageSize)
            return Result<Page<T>>.Failure(new RequestError(RequestErrorKind.Validation,
                $"The {record} list limit must be between 1 and {MaximumPageSize}."));
        var offset = 0;
        if (cursor is not null && (!int.TryParse(cursor, NumberStyles.None,
                CultureInfo.InvariantCulture, out offset) || offset < 1 || offset > items.Count))
            return Result<Page<T>>.Failure(new RequestError(RequestErrorKind.Validation,
                $"The {record} cursor is invalid."));
        var take = limit ?? 50;
        var page = items.Skip(offset).Take(take).ToArray();
        var next = offset + page.Length;
        return Result<Page<T>>.Success(new Page<T>(page,
            next < items.Count ? next.ToString(CultureInfo.InvariantCulture) : null));
    }
}

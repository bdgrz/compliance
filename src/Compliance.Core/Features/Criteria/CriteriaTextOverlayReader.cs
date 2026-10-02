using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria;

/// <summary>Resolves a tenant overlay at read time and applies the requested license use.</summary>
public sealed class CriteriaTextOverlayReader(IAggregateReader reader)
{
    public async ValueTask<Criterion> ApplyAsync(Uuid tenantId, Criterion entry, bool export,
        CancellationToken ct)
    {
        var ledger = await ReadAsync(tenantId, entry.EditionId, ct).ConfigureAwait(false);
        var current = ledger.Get(entry.Identifier);
        return current is null
            ? entry
            : CriteriaTextOverlayPolicy.Apply(entry, current, current, export);
    }

    public async ValueTask<IReadOnlyList<Criterion>> ApplyPageAsync(Uuid tenantId,
        IReadOnlyList<Criterion> entries, bool export, CancellationToken ct)
    {
        if (entries.Count == 0)
            return entries;
        var editionId = entries[0].EditionId;
        if (entries.Any(entry => entry.EditionId != editionId))
            throw new ArgumentException("A criteria overlay page must contain one edition.", nameof(entries));
        var ledger = await ReadAsync(tenantId, editionId, ct).ConfigureAwait(false);
        return entries.Select(entry => ledger.Get(entry.Identifier) is { } current
                ? CriteriaTextOverlayPolicy.Apply(entry, current, current, export)
                : entry)
            .ToArray();
    }

    ValueTask<CriteriaTextOverlayLedger> ReadAsync(Uuid tenantId, Uuid editionId,
        CancellationToken ct) => reader.HydrateAsync(
        new CriteriaTextOverlayLedger(tenantId, editionId), ct);
}

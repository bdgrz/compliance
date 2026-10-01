using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Bdgrz.Compliance.Features.Snapshots;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Builds and orders population rows for the access-review snapshot kinds.</summary>
static class SnapshotRows
{
    public static PopulationRow Row<T>(string key, T value, JsonTypeInfo<T> type) =>
        new(key, JsonSerializer.SerializeToElement(value, type));

    public static void Sort(List<PopulationRow> rows) =>
        rows.Sort(static (left, right) => SnapshotContentIdentity.CompareCodePoints(
            SnapshotContentIdentity.NormalizeText(left.Key),
            SnapshotContentIdentity.NormalizeText(right.Key)));
}
